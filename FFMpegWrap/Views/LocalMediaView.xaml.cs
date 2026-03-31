using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FFMpegWrap.ViewModels;

namespace FFMpegWrap.Views;

public partial class LocalMediaView : UserControl
{
    private LocalMediaViewModel? _viewModel;
    private bool _isPreviewPlaying;
    private MediaElement? PreviewPlayerElement => FindName("PreviewPlayer") as MediaElement;

    public LocalMediaView()
    {
        InitializeComponent();
        if (PreviewPlayerElement is not null)
        {
            PreviewPlayerElement.MediaOpened += OnPreviewPlayerMediaOpened;
            PreviewPlayerElement.MediaEnded += OnPreviewPlayerMediaEnded;
            PreviewPlayerElement.MediaFailed += OnPreviewPlayerMediaFailed;
        }

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdatePreviewSource();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as LocalMediaViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdatePreviewSource();
        }
        else
        {
            PreviewPlayerElement?.Stop();
            _isPreviewPlaying = false;
            if (PreviewPlayerElement is not null)
            {
                PreviewPlayerElement.Source = null;
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LocalMediaViewModel.PreviewSource))
        {
            Dispatcher.Invoke(UpdatePreviewSource);
        }
    }

    private void UpdatePreviewSource()
    {
        if (_viewModel?.PreviewSource is null)
        {
            PreviewPlayerElement?.Stop();
            _isPreviewPlaying = false;
            if (PreviewPlayerElement is not null)
            {
                PreviewPlayerElement.Source = null;
            }
            return;
        }

        if (PreviewPlayerElement is null)
        {
            return;
        }

        PreviewPlayerElement.Stop();
        _isPreviewPlaying = false;
        PreviewPlayerElement.IsMuted = false;
        PreviewPlayerElement.Volume = 1.0;
        PreviewPlayerElement.Source = _viewModel.PreviewSource;
    }

    private void OnPreviewPlayerMediaOpened(object sender, RoutedEventArgs e)
    {
        if (PreviewPlayerElement is null)
        {
            return;
        }

        PreviewPlayerElement.IsMuted = false;
        PreviewPlayerElement.Volume = 1.0;
        PreviewPlayerElement.Position = TimeSpan.Zero;
        PreviewPlayerElement.Play();
        _isPreviewPlaying = true;
    }

    private void OnPreviewPlayerMediaEnded(object sender, RoutedEventArgs e)
    {
        if (PreviewPlayerElement is null)
        {
            return;
        }

        PreviewPlayerElement.Position = TimeSpan.Zero;
        PreviewPlayerElement.Play();
        _isPreviewPlaying = true;
    }

    private void OnPreviewPlayerMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        PreviewPlayerElement?.Stop();
        _isPreviewPlaying = false;
    }

    private void OnRewindPreviewClick(object sender, RoutedEventArgs e)
    {
        SeekPreviewBy(TimeSpan.FromSeconds(-5));
    }

    private void OnForwardPreviewClick(object sender, RoutedEventArgs e)
    {
        SeekPreviewBy(TimeSpan.FromSeconds(5));
    }

    private void OnPlayPausePreviewClick(object sender, RoutedEventArgs e)
    {
        if (PreviewPlayerElement?.Source is null)
        {
            return;
        }

        if (_isPreviewPlaying)
        {
            PreviewPlayerElement.Pause();
            _isPreviewPlaying = false;
            return;
        }

        PreviewPlayerElement.Play();
        _isPreviewPlaying = true;
    }

    private void OnStopPreviewClick(object sender, RoutedEventArgs e)
    {
        if (PreviewPlayerElement is null)
        {
            return;
        }

        PreviewPlayerElement.Stop();
        PreviewPlayerElement.Position = TimeSpan.Zero;
        _isPreviewPlaying = false;
    }

    private void SeekPreviewBy(TimeSpan delta)
    {
        if (PreviewPlayerElement?.Source is null)
        {
            return;
        }

        var targetPosition = PreviewPlayerElement.Position + delta;
        if (targetPosition < TimeSpan.Zero)
        {
            targetPosition = TimeSpan.Zero;
        }

        if (PreviewPlayerElement.NaturalDuration.HasTimeSpan)
        {
            var duration = PreviewPlayerElement.NaturalDuration.TimeSpan;
            if (targetPosition > duration)
            {
                targetPosition = duration;
            }
        }

        PreviewPlayerElement.Position = targetPosition;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        PreviewPlayerElement?.Stop();
        _isPreviewPlaying = false;
    }
}
