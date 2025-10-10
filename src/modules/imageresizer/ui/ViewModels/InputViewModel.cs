#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using Common.UI;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Services;
using ImageResizer.Views;
using Microsoft.Windows.AI.Imaging;

namespace ImageResizer.ViewModels
{
    public class InputViewModel : Observable
    {
        private static WinAiSuperResolutionService _aiSuperResolutionService;

        private readonly ResizeBatch _batch;
        private readonly MainViewModel _mainViewModel;
        private readonly IMainView _mainView;
        private readonly bool _hasMultipleFiles;
        private bool _originalDimensionsLoaded;
        private int? _originalWidth;
        private int? _originalHeight;
        private string _currentResolutionDescription;
        private string _newResolutionDescription;
        private bool _isModelAvailable;
        private bool _isModelDownloading;
        private string _modelStatusMessage;
        private double _modelDownloadProgress;

        public enum Dimension
        {
            Width,
            Height,
        }

        public class KeyPressParams
        {
            public double Value { get; set; }

            public Dimension Dimension { get; set; }
        }

        public InputViewModel(
            Settings settings,
            MainViewModel mainViewModel,
            IMainView mainView,
            ResizeBatch batch)
        {
            _batch = batch;
            _mainViewModel = mainViewModel;
            _mainView = mainView;
            _hasMultipleFiles = _batch?.Files.Count > 1;

            Settings = settings;
            if (settings != null)
            {
                settings.CustomSize.PropertyChanged += (sender, e) => settings.SelectedSize = (CustomSize)sender;
                settings.PropertyChanged += HandleSettingsPropertyChanged;
            }

            ResizeCommand = new RelayCommand(Resize);
            CancelCommand = new RelayCommand(Cancel);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            EnterKeyPressedCommand = new RelayCommand<KeyPressParams>(HandleEnterKeyPress);
            DownloadModelCommand = new RelayCommand(async () => await DownloadModelAsync());

            // Check if AI is already enabled in settings (loaded from previous session)
            if (settings?.UseAiSuperResolution == true)
            {
                // Set initial checking state
                IsModelAvailable = false;
                IsModelDownloading = false;
                ModelStatusMessage = Resources.Input_AiModelChecking;

                // Notify UI that computed properties need to update
                OnPropertyChanged(nameof(ShowModelDownloadPrompt));
                OnPropertyChanged(nameof(ShowAiControls));

                _ = CheckModelAvailabilityAsync();
            }
        }

        public Settings Settings { get; }

        public IEnumerable<ResizeFit> ResizeFitValues => Enum.GetValues<ResizeFit>();

        public IEnumerable<ResizeUnit> ResizeUnitValues => Enum.GetValues<ResizeUnit>();

        public bool IsAiMode => Settings?.UseAiSuperResolution == true;

        public int AiSuperResolutionScale
        {
            get => Settings?.AiSuperResolutionScale ?? 1;
            set
            {
                if (Settings == null || value == Settings.AiSuperResolutionScale)
                {
                    return;
                }

                Settings.AiSuperResolutionScale = value;
            }
        }

        public string AiScaleDisplay => AiSuperResolutionFormatter.FormatScaleName(AiSuperResolutionScale);

        public string AiScaleDescription => FormatLabeledSize(Resources.Input_AiScaleLabel, AiScaleDisplay);

        public string CurrentResolutionDescription
        {
            get => _currentResolutionDescription;
            private set => Set(ref _currentResolutionDescription, value);
        }

        public string NewResolutionDescription
        {
            get => _newResolutionDescription;
            private set => Set(ref _newResolutionDescription, value);
        }

        public bool ShowAiSizeDescriptions => Settings?.UseAiSuperResolution == true && !_hasMultipleFiles;

        public bool IsModelAvailable
        {
            get => _isModelAvailable;
            private set => Set(ref _isModelAvailable, value);
        }

        public bool IsModelDownloading
        {
            get => _isModelDownloading;
            private set => Set(ref _isModelDownloading, value);
        }

        public string ModelStatusMessage
        {
            get => _modelStatusMessage;
            private set => Set(ref _modelStatusMessage, value);
        }

        public double ModelDownloadProgress
        {
            get => _modelDownloadProgress;
            private set => Set(ref _modelDownloadProgress, value);
        }

        public bool ShowModelDownloadPrompt => Settings?.UseAiSuperResolution == true && !IsModelAvailable && !IsModelDownloading;

        public bool ShowAiControls => Settings?.UseAiSuperResolution == true && IsModelAvailable;

        public ICommand ResizeCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand EnterKeyPressedCommand { get; private set; }

        public ICommand DownloadModelCommand { get; private set; }

        // Any of the files is a gif
        public bool TryingToResizeGifFiles =>
                _batch?.Files.Any(filename => filename.EndsWith(".gif", System.StringComparison.InvariantCultureIgnoreCase)) == true;

        public void Resize()
        {
            Settings.Save();
            _mainViewModel.CurrentPage = new ProgressViewModel(_batch, _mainViewModel, _mainView);
        }

        public static void OpenSettings()
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ImageResizer, false);
        }

        private void HandleEnterKeyPress(KeyPressParams parameters)
        {
            switch (parameters.Dimension)
            {
                case Dimension.Width:
                    Settings.CustomSize.Width = parameters.Value;
                    break;
                case Dimension.Height:
                    Settings.CustomSize.Height = parameters.Value;
                    break;
            }
        }

        public void Cancel()
            => _mainView.Close();

        private void HandleSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Settings.UseAiSuperResolution):
                    if (Settings.UseAiSuperResolution)
                    {
                        if (Settings.AiSuperResolutionScale != 2)
                        {
                            Settings.AiSuperResolutionScale = 2;
                        }

                        // Set initial checking state before async call
                        IsModelAvailable = false;
                        IsModelDownloading = false;
                        ModelStatusMessage = Resources.Input_AiModelChecking;
                        _ = CheckModelAvailabilityAsync();
                    }
                    else if (Settings.Sizes.Count > 0 && Settings.SelectedSizeIndex != 0)
                    {
                        Settings.SelectedSizeIndex = 0;
                    }

                    EnsureAiScaleWithinRange();
                    OnPropertyChanged(nameof(IsAiMode));
                    OnPropertyChanged(nameof(ShowAiSizeDescriptions));
                    OnPropertyChanged(nameof(ShowModelDownloadPrompt));
                    OnPropertyChanged(nameof(ShowAiControls));
                    OnPropertyChanged(nameof(AiScaleDisplay));
                    OnPropertyChanged(nameof(AiScaleDescription));
                    OnPropertyChanged(nameof(AiSuperResolutionScale));
                    UpdateAiDetails();
                    break;

                case nameof(Settings.AiSuperResolutionScale):
                    EnsureAiScaleWithinRange();
                    OnPropertyChanged(nameof(AiScaleDisplay));
                    OnPropertyChanged(nameof(AiScaleDescription));
                    OnPropertyChanged(nameof(AiSuperResolutionScale));
                    UpdateAiDetails();
                    break;

                case nameof(Settings.SelectedSizeIndex):
                case nameof(Settings.SelectedSize):
                    if (!Settings.UseAiSuperResolution)
                    {
                        UpdateAiDetails();
                    }

                    break;
            }
        }

        private void EnsureAiScaleWithinRange()
        {
            if (Settings == null)
            {
                return;
            }

            if (Settings.AiSuperResolutionScale < 1 || Settings.AiSuperResolutionScale > 8)
            {
                Settings.AiSuperResolutionScale = 2;
            }
        }

        private void UpdateAiDetails()
        {
            if (Settings == null || !Settings.UseAiSuperResolution)
            {
                CurrentResolutionDescription = string.Empty;
                NewResolutionDescription = string.Empty;
                return;
            }

            EnsureAiScaleWithinRange();

            if (_hasMultipleFiles)
            {
                CurrentResolutionDescription = string.Empty;
                NewResolutionDescription = string.Empty;
                return;
            }

            EnsureOriginalDimensionsLoaded();

            var hasConcreteSize = _originalWidth.HasValue && _originalHeight.HasValue;
            var currentValue = hasConcreteSize
                ? FormatDimensions(_originalWidth!.Value, _originalHeight!.Value)
                : Resources.Input_AiUnknownSize;
            CurrentResolutionDescription = FormatLabeledSize(Resources.Input_AiCurrentLabel, currentValue);

            var scale = Settings.AiSuperResolutionScale;
            var newValue = hasConcreteSize
                ? FormatDimensions((long)_originalWidth!.Value * scale, (long)_originalHeight!.Value * scale)
                : Resources.Input_AiUnknownSize;
            NewResolutionDescription = FormatLabeledSize(Resources.Input_AiNewLabel, newValue);
        }

        private static string FormatDimensions(long width, long height)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0} × {1}", width, height);
        }

        private static string FormatLabeledSize(string label, string value)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}: {1}", label, value);
        }

        private void EnsureOriginalDimensionsLoaded()
        {
            if (_originalDimensionsLoaded)
            {
                return;
            }

            var file = _batch?.Files.FirstOrDefault();
            if (string.IsNullOrEmpty(file))
            {
                _originalDimensionsLoaded = true;
                return;
            }

            try
            {
                using var stream = File.OpenRead(file);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                var frame = decoder.Frames.FirstOrDefault();
                if (frame != null)
                {
                    _originalWidth = frame.PixelWidth;
                    _originalHeight = frame.PixelHeight;
                }
            }
            catch (IOException)
            {
                _originalWidth = null;
                _originalHeight = null;
            }
            catch (NotSupportedException)
            {
                _originalWidth = null;
                _originalHeight = null;
            }
            catch (Exception)
            {
                _originalWidth = null;
                _originalHeight = null;
            }
            finally
            {
                _originalDimensionsLoaded = true;
            }
        }

        private async Task CheckModelAvailabilityAsync()
        {
            try
            {
                // Check state using static method
                // Following the pattern from sample project (Sample.xaml.cs:31-52)
                var readyState = WinAiSuperResolutionService.GetModelReadyState();

                // Only Ready or NotReady states allow using AI functionality
                // EnsureReadyAsync can only be called safely in these states
                if (readyState is Microsoft.Windows.AI.AIFeatureReadyState.Ready
                               or Microsoft.Windows.AI.AIFeatureReadyState.NotReady)
                {
                    if (readyState == Microsoft.Windows.AI.AIFeatureReadyState.Ready)
                    {
                        // Model is ready, initialize ImageScaler on UI thread
                        IsModelAvailable = true;
                        ModelStatusMessage = string.Empty;
                        await InitializeAiServiceAsync();
                    }
                    else
                    {
                        // NotReady
                        // Model not downloaded, show download button
                        IsModelAvailable = false;
                        ModelStatusMessage = Resources.Input_AiModelNotAvailable;
                    }
                }
                else
                {
                    // System doesn't support AI (DisabledByUser or NotSupported)
                    // Don't call any AI APIs, just show error message
                    IsModelAvailable = false;

                    if (readyState == Microsoft.Windows.AI.AIFeatureReadyState.DisabledByUser)
                    {
                        ModelStatusMessage = Resources.Input_AiModelDisabledByUser;
                    }
                    else
                    {
                        ModelStatusMessage = Resources.Input_AiModelNotSupported;
                    }
                }

                OnPropertyChanged(nameof(ShowModelDownloadPrompt));
                OnPropertyChanged(nameof(ShowAiControls));
            }
            catch (Exception)
            {
                // GetReadyState failed, system doesn't support AI
                IsModelAvailable = false;
                ModelStatusMessage = Resources.Input_AiModelNotSupported;
                OnPropertyChanged(nameof(ShowModelDownloadPrompt));
                OnPropertyChanged(nameof(ShowAiControls));
            }
        }

        private async Task InitializeAiServiceAsync()
        {
            try
            {
                // Create service instance if not already created
                if (_aiSuperResolutionService == null)
                {
                    _aiSuperResolutionService = new WinAiSuperResolutionService();
                }

                // Initialize ImageScaler on UI thread (async method)
                // This follows the pattern from the sample project
                var success = await _aiSuperResolutionService.InitializeAsync();

                if (success)
                {
                    // Set the initialized service to ResizeBatch
                    ResizeBatch.SetAiSuperResolutionService(_aiSuperResolutionService);
                }
                else
                {
                    // Failed to initialize, use NoOp service
                    ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);
                }
            }
            catch (Exception)
            {
                // Failed to initialize, use NoOp service
                ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);
            }
        }

        private async Task DownloadModelAsync()
        {
            try
            {
                IsModelDownloading = true;
                ModelDownloadProgress = 0;
                ModelStatusMessage = Resources.Input_AiModelDownloading;
                OnPropertyChanged(nameof(ShowModelDownloadPrompt));

                // Call EnsureReadyAsync to download and prepare model
                // This is safe because we already checked state is Ready or NotReady
                // Following sample project pattern (Sample.xaml.cs:36)
                var result = await WinAiSuperResolutionService.EnsureModelReadyAsync();

                if (result?.Status == Microsoft.Windows.AI.AIFeatureReadyResultState.Success)
                {
                    // Model downloaded and ready
                    IsModelAvailable = true;
                    ModelStatusMessage = string.Empty;

                    // Initialize ImageScaler instance on UI thread
                    await InitializeAiServiceAsync();
                }
                else
                {
                    // Download failed
                    IsModelAvailable = false;
                    ModelStatusMessage = Resources.Input_AiModelDownloadFailed;
                }
            }
            catch (Exception)
            {
                IsModelAvailable = false;
                ModelStatusMessage = Resources.Input_AiModelDownloadFailed;
            }
            finally
            {
                IsModelDownloading = false;
                ModelDownloadProgress = 0;
                OnPropertyChanged(nameof(ShowModelDownloadPrompt));
                OnPropertyChanged(nameof(ShowAiControls));
            }
        }
    }
}
