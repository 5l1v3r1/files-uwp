﻿using Files.Filesystem;
using Files.Interacts;
using Files.View_Models;
using Files.Views;
using Files.Views.Pages;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Files.UserControls
{
    public sealed partial class ModernNavigationToolbar : UserControl, INavigationToolbar, INotifyPropertyChanged
    {
        public SettingsViewModel AppSettings => App.AppSettings;

        public ModernNavigationToolbar()
        {
            this.InitializeComponent();

            MainPage.OnTabItemDraggedOver += MainPage_OnTabItemDraggedOver;
        }

        private void MainPage_OnTabItemDraggedOver(object sender, bool e)
        {
            VerticalTabViewFlyout.ShowAt(VerticalTabStripInvokeButton);
        }

        private bool manualEntryBoxLoaded = false;

        private bool ManualEntryBoxLoaded
        {
            get
            {
                return manualEntryBoxLoaded;
            }
            set
            {
                if (value != manualEntryBoxLoaded)
                {
                    manualEntryBoxLoaded = value;
                    NotifyPropertyChanged("ManualEntryBoxLoaded");
                }
            }
        }

        private bool clickablePathLoaded = true;

        private bool ClickablePathLoaded
        {
            get
            {
                return clickablePathLoaded;
            }
            set
            {
                if (value != clickablePathLoaded)
                {
                    clickablePathLoaded = value;
                    NotifyPropertyChanged("ClickablePathLoaded");
                }
            }
        }

        private bool SearchBoxLoaded { get; set; } = false;
        private string PathText { get; set; }

        bool INavigationToolbar.IsSearchReigonVisible
        {
            get
            {
                return SearchBoxLoaded;
            }
            set
            {
                if (value)
                {
                    ToolbarGrid.ColumnDefinitions[2].MinWidth = 285;
                    ToolbarGrid.ColumnDefinitions[2].Width = GridLength.Auto;
                    SearchBoxLoaded = true;
                }
                else
                {
                    ToolbarGrid.ColumnDefinitions[2].MinWidth = 0;
                    ToolbarGrid.ColumnDefinitions[2].Width = new GridLength(0);
                    SearchBoxLoaded = false;
                }
            }
        }

        bool INavigationToolbar.IsEditModeEnabled
        {
            get
            {
                return ManualEntryBoxLoaded;
            }
            set
            {
                if (value)
                {
                    ManualEntryBoxLoaded = true;
                    ClickablePathLoaded = false;
                }
                else
                {
                    ManualEntryBoxLoaded = false;
                    ClickablePathLoaded = true;
                }
            }
        }

        bool INavigationToolbar.CanRefresh
        {
            get
            {
                return Refresh.IsEnabled;
            }
            set
            {
                Refresh.IsEnabled = value;
            }
        }

        bool INavigationToolbar.CanNavigateToParent
        {
            get
            {
                return Up.IsEnabled;
            }
            set
            {
                Up.IsEnabled = value;
            }
        }

        bool INavigationToolbar.CanGoBack
        {
            get
            {
                return Back.IsEnabled;
            }
            set
            {
                Back.IsEnabled = value;
            }
        }

        bool INavigationToolbar.CanGoForward
        {
            get
            {
                return Forward.IsEnabled;
            }
            set
            {
                Forward.IsEnabled = value;
            }
        }

        string INavigationToolbar.PathControlDisplayText
        {
            get
            {
                return PathText;
            }
            set
            {
                PathText = value;
                NotifyPropertyChanged("PathText");
            }
        }

        private readonly ObservableCollection<PathBoxItem> pathComponents = new ObservableCollection<PathBoxItem>();

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        ObservableCollection<PathBoxItem> INavigationToolbar.PathComponents => pathComponents;

        public UserControl MultiTaskingControl => verticalTabs;

        private void ManualPathEntryItem_Click(object sender, RoutedEventArgs e)
        {
            (this as INavigationToolbar).IsEditModeEnabled = true;
            VisiblePath.Focus(FocusState.Programmatic);
            VisiblePath.SelectAll();
        }

        private void VisiblePath_TextChanged(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var PathBox = (sender as TextBox);
                CheckPathInput(App.CurrentInstance.FilesystemViewModel, PathBox.Text);
                App.CurrentInstance.NavigationToolbar.IsEditModeEnabled = false;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                App.CurrentInstance.NavigationToolbar.IsEditModeEnabled = false;
            }
        }

        public async void CheckPathInput(ItemViewModel instance, string CurrentInput)
        {
            if (CurrentInput != instance.WorkingDirectory || App.CurrentInstance.ContentFrame.CurrentSourcePageType == typeof(YourHome))
            {
                //(App.CurrentInstance.OperationsControl as RibbonArea).RibbonViewModel.HomeItems.isEnabled = false;
                //(App.CurrentInstance.OperationsControl as RibbonArea).RibbonViewModel.ShareItems.isEnabled = false;

                if (CurrentInput.Equals("Home", StringComparison.OrdinalIgnoreCase) || CurrentInput.Equals(ResourceController.GetTranslation("NewTab"), StringComparison.OrdinalIgnoreCase))
                {
                    await App.CurrentInstance.FilesystemViewModel.SetWorkingDirectory(ResourceController.GetTranslation("NewTab"));
                    App.CurrentInstance.ContentFrame.Navigate(typeof(YourHome), ResourceController.GetTranslation("NewTab"), new SuppressNavigationTransitionInfo());
                }
                else
                {
                    var workingDir = string.IsNullOrEmpty(App.CurrentInstance.FilesystemViewModel.WorkingDirectory)
                        ? AppSettings.HomePath
                        : App.CurrentInstance.FilesystemViewModel.WorkingDirectory;
                    var parentItem = await StorageFileExtensions.GetFolderWithPathFromPathAsync(workingDir);

                    if (CurrentInput.Contains("%temp%")) CurrentInput = CurrentInput.Replace("%temp%", AppSettings.TempPath);
                    if (CurrentInput.Contains("%homepath%")) CurrentInput = CurrentInput.Replace("%homepath%", AppSettings.HomePath);
                    CurrentInput = Environment.ExpandEnvironmentVariables(CurrentInput);

                    var item = await DrivesManager.GetRootFromPath(CurrentInput);
                    try
                    {
                        var pathToNavigate = (await StorageFileExtensions.GetFolderFromPathAsync(CurrentInput, item, parentItem)).Path;

                        App.CurrentInstance.ContentFrame.Navigate(AppSettings.GetLayoutType(), pathToNavigate); // navigate to folder
                    }
                    catch (Exception) // Not a folder or inaccessible
                    {
                        try
                        {
                            var pathToInvoke = (await StorageFileExtensions.GetFileFromPathAsync(CurrentInput, item, parentItem)).Path;
                            await Interaction.InvokeWin32Component(pathToInvoke);
                        }
                        catch (Exception ex) // Not a file or not accessible
                        {
                            // Launch terminal application if possible
                            foreach (var terminal in AppSettings.TerminalController.Model.Terminals)
                            {
                                if (terminal.Path.Equals(CurrentInput, StringComparison.OrdinalIgnoreCase) || terminal.Path.Equals(CurrentInput + ".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (App.Connection != null)
                                    {
                                        var value = new ValueSet
                                        {
                                            { "WorkingDirectory", workingDir },
                                            { "Application", terminal.Path },
                                            { "Arguments", string.Format(terminal.Arguments,
                                            Helpers.PathNormalization.NormalizePath(App.CurrentInstance.FilesystemViewModel.WorkingDirectory)) }
                                        };
                                        await App.Connection.SendMessageAsync(value);
                                    }
                                    return;
                                }
                            }

                            var dialog = new ContentDialog()
                            {
                                Title = "Invalid item",
                                Content = "The item referenced is either invalid or inaccessible.\nMessage:\n\n" + ex.Message,
                                CloseButtonText = "OK"
                            };

                            await dialog.ShowAsync();
                        }
                    }
                }

                App.CurrentInstance.NavigationToolbar.PathControlDisplayText = App.CurrentInstance.FilesystemViewModel.WorkingDirectory;
            }
        }

        private void VisiblePath_LostFocus(object sender, RoutedEventArgs e)
        {
            if (FocusManager.GetFocusedElement() is FlyoutBase || FocusManager.GetFocusedElement() is AppBarButton || FocusManager.GetFocusedElement() is Popup) { return; }

            var element = FocusManager.GetFocusedElement();
            var elementAsControl = element as Control;

            if (elementAsControl.FocusState != FocusState.Programmatic && elementAsControl.FocusState != FocusState.Keyboard)
            {
                App.CurrentInstance.NavigationToolbar.IsEditModeEnabled = false;
            }
            else
            {
                if (App.CurrentInstance.NavigationToolbar.IsEditModeEnabled)
                {
                    this.VisiblePath.Focus(FocusState.Programmatic);
                }
            }
        }

        private void PathViewInteract_ItemClick(object sender, ItemClickEventArgs e)
        {
            var itemTappedPath = (e.ClickedItem as PathBoxItem).Path.ToString();
            if (itemTappedPath == "Home" || itemTappedPath == ResourceController.GetTranslation("NewTab"))
                return;

            App.CurrentInstance.ContentFrame.Navigate(AppSettings.GetLayoutType(), itemTappedPath); // navigate to folder
        }

        private async void Button_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                e.Handled = true;
                cancelFlyoutOpen = false;
                await Task.Delay(1000);
                if (!cancelFlyoutOpen)
                {
                    (sender as Button).Flyout.ShowAt(sender as Button);
                    cancelFlyoutOpen = false;
                }
                else
                {
                    cancelFlyoutOpen = false;
                }
            }
        }

        private bool cancelFlyoutOpen = false;

        private void VerticalTabStripInvokeButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                e.Handled = true;
                if (!(sender as Button).Flyout.IsOpen)
                {
                    cancelFlyoutOpen = true;
                }
            }
        }

        private void Button_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
            (sender as Button).Flyout.ShowAt(sender as Button);
        }

        private void Flyout_Opened(object sender, object e)
        {
            VisualStateManager.GoToState(VerticalTabStripInvokeButton, "PointerOver", false);
        }

        private void Flyout_Closed(object sender, object e)
        {
            VisualStateManager.GoToState(VerticalTabStripInvokeButton, "Normal", false);
        }

        private void VerticalTabStripInvokeButton_DragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
            (sender as Button).Flyout.ShowAt(sender as Button);
        }

        private bool cancelFlyoutAutoClose = false;

        private async void verticalTabs_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                e.Handled = true;
                cancelFlyoutAutoClose = false;
                verticalTabs.PointerEntered += verticalTabs_PointerEntered;
                await Task.Delay(1000);
                verticalTabs.PointerEntered -= verticalTabs_PointerEntered;
                if (!cancelFlyoutAutoClose)
                {
                    VerticalTabViewFlyout.Hide();
                }
                cancelFlyoutAutoClose = false;
            }
        }

        private void verticalTabs_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                e.Handled = true;
                cancelFlyoutAutoClose = true;
            }
        }
    }
}