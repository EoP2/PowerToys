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
using System.Windows.Input;
using System.Windows.Media.Imaging;

using Common.UI;
using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Views;

namespace ImageResizer.ViewModels
{
    public class InputViewModel : Observable
    {
        private readonly ResizeBatch _batch;
        private readonly MainViewModel _mainViewModel;
        private readonly IMainView _mainView;
        private readonly bool _hasMultipleFiles;
        private bool _originalDimensionsLoaded;
        private int? _originalWidth;
        private int? _originalHeight;
        private string _currentResolutionDescription;
        private string _newResolutionDescription;

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

            if (Settings?.UseAiSuperResolution == true)
            {
                UpdateAiDetails();
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

        public ICommand ResizeCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand EnterKeyPressedCommand { get; private set; }

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
                    }
                    else if (Settings.Sizes.Count > 0 && Settings.SelectedSizeIndex != 0)
                    {
                        Settings.SelectedSizeIndex = 0;
                    }

                    EnsureAiScaleWithinRange();
                    OnPropertyChanged(nameof(IsAiMode));
                    OnPropertyChanged(nameof(ShowAiSizeDescriptions));
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
    }
}
