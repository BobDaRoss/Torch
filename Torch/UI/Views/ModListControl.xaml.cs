﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using NLog;
using Torch.Collections;
using Torch.Managers;
using Torch.Properties;
using Torch.UI.ViewModels;
using VRage.Game;

namespace Torch.UI.Views
{
    /// <summary>
    ///     Interaction logic for ModListControl.xaml
    /// </summary>
    public partial class ModListControl : UserControl, INotifyPropertyChanged
    {
        private static readonly Logger Log = LogManager.GetLogger(nameof(ModListControl));
        ModItemInfo _draggedMod;
        private readonly InstanceManager _instanceManager;
        bool _isSortedByLoadOrder = true;

        //private List<BindingExpression> _bindingExpressions = new List<BindingExpression>();
        /// <summary>
        ///     Constructor for ModListControl
        /// </summary>
        public ModListControl()
        {
            InitializeComponent();
            _instanceManager = TorchBase.Instance.Managers.GetManager<InstanceManager>();
            _instanceManager.InstanceLoaded += _instanceManager_InstanceLoaded;
            //var mods = _instanceManager.DedicatedConfig?.Mods;
            //if( mods != null)
            //    DataContext = new ObservableCollection<MyObjectBuilder_Checkpoint.ModItem>();
            DataContext = _instanceManager.DedicatedConfig?.Mods;

            // Gets called once all children are loaded
            //Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyStyles));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void ModListControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void ResetSorting()
        {
            CollectionViewSource.GetDefaultView(ModList.ItemsSource).SortDescriptions.Clear();
        }

        private void _instanceManager_InstanceLoaded(ConfigDedicatedViewModel obj)
        {
            Log.Info("Instance loaded.");
            Dispatcher.Invoke(() =>
            {
                DataContext = obj?.Mods ?? new MtObservableList<ModItemInfo>();
                UpdateLayout();
                ((MtObservableList<ModItemInfo>)DataContext).CollectionChanged += OnModlistUpdate;
            });
        }

        private void OnModlistUpdate(object sender, NotifyCollectionChangedEventArgs e)
        {
            ModList.Items.Refresh();
            //if (e.Action == NotifyCollectionChangedAction.Remove)
            //    _instanceManager.SaveConfig();
        }

        private void SaveBtn_OnClick(object sender, RoutedEventArgs e)
        {
            _instanceManager.SaveConfig();
        }

        private void AddBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (TryExtractId(AddModIDTextBox.Text, out var id))
            {
                var mod = new ModItemInfo(new MyObjectBuilder_Checkpoint.ModItem(id));
                //mod.PublishedFileId = id;
                _instanceManager.DedicatedConfig.Mods.Add(mod);
                Task.Run(mod.UpdateModInfoAsync)
                    .ContinueWith((t) => { Dispatcher.Invoke(() => { _instanceManager.DedicatedConfig.Save(); }); });
                AddModIDTextBox.Text = "";
            }
            else
            {
                AddModIDTextBox.BorderBrush = Brushes.Red;
                Log.Warn("Invalid mod id!");
                MessageBox.Show("Invalid mod id!");
            }
        }

        private void RemoveBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var modList = (MtObservableList<ModItemInfo>)DataContext;
            if (ModList.SelectedItem is ModItemInfo mod && modList.Contains(mod))
                modList.Remove(mod);
        }

        private bool TryExtractId(string input, out ulong result)
        {
            var match = Regex.Match(input, @"(?<=id=)\d+").Value;

            bool success;
            if (string.IsNullOrEmpty(match))
                success = ulong.TryParse(input, out result);
            else
                success = ulong.TryParse(match, out result);

            return success;
        }

        private void ModList_Sorting(object sender, DataGridSortingEventArgs e)
        {
            Log.Info($"Sorting by '{e.Column.Header}'");
            if (e.Column == ModList.Columns[0])
            {
                var dataView = CollectionViewSource.GetDefaultView(ModList.ItemsSource);
                dataView.SortDescriptions.Clear();
                dataView.Refresh();
                _isSortedByLoadOrder = true;
            }
            else
                _isSortedByLoadOrder = false;
        }

        private void ModList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //return;

            _draggedMod = (ModItemInfo)TryFindRowAtPoint((UIElement)sender, e.GetPosition(ModList))?.DataContext;

            //DraggedMod = (ModItemInfo) ModList.SelectedItem;
        }

        private static DataGridRow TryFindRowAtPoint(UIElement reference, Point point)
        {
            var element = reference.InputHitTest(point) as DependencyObject;
            if (element == null)
                return null;
            if (element is DataGridRow row)
                return row;
            else
                return TryFindParent<DataGridRow>(element);
        }

        private static T TryFindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent;
            if (child == null)
                return null;

            if (child is ContentElement contentElement)
            {
                parent = ContentOperations.GetParent(contentElement);
                if (parent == null && child is FrameworkContentElement fce)
                    parent = fce.Parent;
            }
            else
            {
                parent = VisualTreeHelper.GetParent(child);
            }

            if (parent is T result)
                return result;
            else
                return TryFindParent<T>(parent);
        }

        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedMod == null)
                return;

            if (!_isSortedByLoadOrder)
                return;

            var targetMod = (ModItemInfo)TryFindRowAtPoint((UIElement)sender, e.GetPosition(ModList))?.DataContext;
            if (targetMod != null && !ReferenceEquals(_draggedMod, targetMod))
            {
                var modList = (MtObservableList<ModItemInfo>)DataContext;
                modList.Move(modList.IndexOf(targetMod), _draggedMod);
                //modList.RemoveAt(modList.IndexOf(_draggedMod));
                //modList.Insert(modList.IndexOf(targetMod), _draggedMod);
                ModList.Items.Refresh();
                ModList.SelectedItem = _draggedMod;
            }
        }

        private void ModList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSortedByLoadOrder)
            {
                var targetMod = (ModItemInfo)TryFindRowAtPoint((UIElement)sender, e.GetPosition(ModList))?.DataContext;
                if (targetMod != null && !ReferenceEquals(_draggedMod, targetMod))
                {
                    var msg = "Drag and drop is only available when sorted by load order!";
                    Log.Warn(msg);
                    MessageBox.Show(msg);
                }
            }

            //if (DraggedMod != null && HasOrderChanged)
            //Log.Info("Dragging over, saving...");
            //_instanceManager.SaveConfig();
            _draggedMod = null;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ModList_Selected(object sender, SelectedCellsChangedEventArgs e)
        {
            if (_draggedMod != null)
                ModList.SelectedItem = _draggedMod;
            else if (e.AddedCells.Count > 0)
                ModList.SelectedItem = e.AddedCells[0].Item;
        }

        private void BulkButton_OnClick(object sender, RoutedEventArgs e)
        {
            var editor = new CollectionEditor();

            //let's see just how poorly we can do this
            var modList = ((MtObservableList<ModItemInfo>)DataContext).ToList();
            var idList = modList.Select(m => m.PublishedFileId).ToList();
            var tasks = new List<Task>();
            //blocking
            editor.Edit<ulong>(idList, "Mods");

            modList.RemoveAll(m => !idList.Contains(m.PublishedFileId));
            foreach (var id in idList)
            {
                if (!modList.Any(m => m.PublishedFileId == id))
                {
                    var mod = new ModItemInfo(new MyObjectBuilder_Checkpoint.ModItem(id));
                    tasks.Add(Task.Run(mod.UpdateModInfoAsync));
                    modList.Add(mod);
                }
            }

            _instanceManager.DedicatedConfig.Mods.Clear();
            foreach (var mod in modList)
                _instanceManager.DedicatedConfig.Mods.Add(mod);

            if (tasks.Any())
                Task.WaitAll(tasks.ToArray());

            Dispatcher.Invoke(() => { _instanceManager.DedicatedConfig.Save(); });
        }
    }
}