﻿// Licensed to the Blazor Desktop Contributors under one or more agreements.
// The Blazor Desktop Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using BlazorDesktop.Hosting;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Web.WebView2.Core;
using Windows.UI.ViewManagement;

namespace BlazorDesktop.Wpf;

/// <summary>
/// The blazor desktop window.
/// </summary>
public class BlazorDesktopWindow : Window
{
    /// <summary>
    /// The <see cref="BlazorWebView"/>.
    /// </summary>
    public BlazorWebView WebView { get; }

    /// <summary>
    /// The <see cref="Border"/> for the <see cref="BlazorWebView"/>.
    /// </summary>
    public Border WebViewBorder { get; }

    /// <summary>
    /// The service provider.
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// The configuration.
    /// </summary>
    private readonly IConfiguration _config;

    /// <summary>
    /// The hosting environment.
    /// </summary>
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// The UI settings.
    /// </summary>
    private readonly UISettings _uiSettings;

    /// <summary>
    /// The drag script.
    /// </summary>
    private const string DragScript =
@"
window.addEventListener('DOMContentLoaded', () => {
    document.body.addEventListener('mousedown', evt => {
        const { target } = evt;
        const appRegion = getComputedStyle(target)['-webkit-app-region'];

        if (appRegion === 'drag') {
            chrome.webview.hostObjects.sync.eventForwarder.MouseDownDrag();
            evt.preventDefault();
            evt.stopPropagation();
        }
    });
});
";

    /// <summary>
    /// Creates a <see cref="BlazorDesktopWindow"/> instance.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    public BlazorDesktopWindow(IServiceProvider services, IConfiguration config, IWebHostEnvironment environment)
    {
        WebView = new BlazorWebView();
        WebViewBorder = new Border();
        _services = services;
        _config = config;
        _environment = environment;
        _uiSettings = new UISettings();

        InitializeWindow();
        InitializeWebViewBorder();
        InitializeWebView();
        InitializeTheming();
    }

    /// <summary>
    /// Initializes the window.
    /// </summary>
    private void InitializeWindow()
    {
        Name = "BlazorDesktopWindow";
        Title = _config.GetValue<string?>(WindowDefaults.Title) ?? _environment.ApplicationName;
        Height = _config.GetValue<int?>(WindowDefaults.Height) ?? 768;
        Width = _config.GetValue<int?>(WindowDefaults.Width) ?? 1366;
        MinHeight = _config.GetValue<int?>(WindowDefaults.MinHeight) ?? 0;
        MinWidth = _config.GetValue<int?>(WindowDefaults.MinWidth) ?? 0;
        MaxHeight = _config.GetValue<int?>(WindowDefaults.MaxHeight) ?? double.PositiveInfinity;
        MaxWidth = _config.GetValue<int?>(WindowDefaults.MaxWidth) ?? double.PositiveInfinity;
        UseFrame(_config.GetValue<bool?>(WindowDefaults.Frame) ?? true);
        ResizeMode = (_config.GetValue<bool?>(WindowDefaults.Resizable) ?? true) ? ResizeMode.CanResize : ResizeMode.NoResize;
        UseIcon(_config.GetValue<string?>(WindowDefaults.Icon) ?? string.Empty);
        Content = WebViewBorder;
        StateChanged += WindowStateChanged;
    }

    /// <summary>
    /// Initializes the web view border.
    /// </summary>
    private void InitializeWebViewBorder()
    {
        WebViewBorder.Name = "BlazorDesktopWebViewBorder";
        WebViewBorder.BorderThickness = new Thickness(0, 0, 0, 0);
        WebViewBorder.Child = WebView;
    }

    /// <summary>
    /// Initializes the web view.
    /// </summary>
    private void InitializeWebView()
    {
        WebView.Name = "BlazorDesktopWebView";
        WebView.HostPage = Path.Combine(_environment.WebRootPath, "index.html");
        WebView.Services = _services;

        foreach (var rootComponent in _services.GetRequiredService<RootComponentMappingCollection>())
        {
            WebView.RootComponents.Add(new()
            {
                Selector = rootComponent.Selector,
                ComponentType = rootComponent.ComponentType,
                Parameters = (IDictionary<string, object?>)rootComponent.Parameters.ToDictionary()
            });
        }

        WebView.Loaded += WebViewLoaded;
    }

    /// <summary>
    /// Initializes theming.
    /// </summary>
    private void InitializeTheming()
    {
        SourceInitialized += WindowSourceInitialized;
        _uiSettings.ColorValuesChanged += ThemeChanged;
    }

    /// <summary>
    /// Occurs when the window state changes.
    /// </summary>
    /// <param name="sender">The sending object.</param>
    /// <param name="e">The arguments.</param>
    private void WindowStateChanged(object? sender, EventArgs e)
    {
        var useFrame = _config.GetValue<bool?>(WindowDefaults.Frame) ?? true;

        if (WindowState == WindowState.Maximized && !useFrame)
        {
            WebViewBorder.BorderThickness = new Thickness(8, 8, 8, 0);
        }
        else
        {
            WebViewBorder.BorderThickness = new Thickness(0, 0, 0, 0);
        }
    }

    /// <summary>
    /// Occurs when the window source has initialized.
    /// </summary>
    /// <param name="sender">The sending object.</param>
    /// <param name="e">The arguments.</param>
    private void WindowSourceInitialized(object? sender, EventArgs e)
    {
        UpdateTheme();
    }

    /// <summary>
    /// Occurs when the theme changes.
    /// </summary>
    /// <param name="sender">The sending object.</param>
    /// <param name="args">The arguments.</param>
    private void ThemeChanged(UISettings sender, object args)
    {
        Dispatcher.Invoke(new(UpdateTheme));
    }

    /// <summary>
    /// Update the current theme to match the system.
    /// </summary>
    private void UpdateTheme()
    {
        if (ShouldSystemUseDarkMode())
        {
            _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, new int[] { 1 }, Marshal.SizeOf(typeof(int)));
            Background = new SolidColorBrush(Color.FromRgb(25, 25, 25));
        }
        else
        {
            _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, new int[] { 0 }, Marshal.SizeOf(typeof(int)));
            Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        }
    }

    /// <summary>
    /// If a frame should be used.
    /// </summary>
    /// <param name="frame">If the frame should be used.</param>
    private void UseFrame(bool frame)
    {
        if (!frame)
        {
            WindowChrome.SetWindowChrome(this, new() { NonClientFrameEdges = NonClientFrameEdges.Bottom | NonClientFrameEdges.Left | NonClientFrameEdges.Right });
        }
    }

    /// <summary>
    /// Uses an icon.
    /// </summary>
    /// <param name="icon">The icon string</param>
    private void UseIcon(string icon)
    {
        var defaultIconPath = Path.Combine(_environment.WebRootPath, "favicon.ico");
        var userIconPath = Path.Combine(_environment.WebRootPath, icon);

        if (File.Exists(userIconPath))
        {
            Icon = BitmapFrame.Create(new Uri(userIconPath, UriKind.RelativeOrAbsolute));
        }
        else
        {
            if (File.Exists(defaultIconPath))
            {
                Icon = BitmapFrame.Create(new Uri(defaultIconPath, UriKind.RelativeOrAbsolute));
            }
        }
    }

    /// <summary>
    /// Occurs when the web view has loaded.
    /// </summary>
    /// <param name="sender">The sending object.</param>
    /// <param name="e">The arguments.</param>
    private void WebViewLoaded(object sender, RoutedEventArgs e)
    {
        WebView.WebView.CoreWebView2InitializationCompleted += WebViewInitializationCompleted;
    }

    /// <summary>
    /// Occurs when the web view has finished initialization.
    /// </summary>
    /// <param name="sender">The sending object.</param>
    /// <param name="e">The arguments.</param>
    private void WebViewInitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        var eventForwarder = new BlazorDesktopEventForwarder(new WindowInteropHelper(this).Handle);

        WebView.WebView.CoreWebView2.AddHostObjectToScript("eventForwarder", eventForwarder);
        WebView.WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(DragScript);
        WebView.WebView.CoreWebView2.NavigationCompleted += BlazorWebViewNavigationCompleted;

        WebView.WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
    }

    /// <summary>
    /// Occurs when the web view has finished navigating.
    /// </summary>
    /// <param name="sender">The sending object.</param>
    /// <param name="e">The arguments.</param>
    private void BlazorWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.SingleBorderWindow;
        WebView.WebView.CoreWebView2.NavigationCompleted -= BlazorWebViewNavigationCompleted;
    }

    /// <summary>
    /// Determines if apps should use dark mode.
    /// </summary>
    /// <returns>True if they should, otherwise false.</returns>
    [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")]
    private static extern bool ShouldSystemUseDarkMode();

    /// <summary>
    /// Sets the value of Desktop Window Manager (DWM) non-client rendering attributes for a window.
    /// </summary>
    /// <param name="hwnd">The handle to the window for which the attribute value is to be set.</param>
    /// <param name="dwAttribute">A flag describing which value to set, specified as a value of the DWMWINDOWATTRIBUTE enumeration.</param>
    /// <param name="pvAttribute">A pointer to an object containing the attribute value to set.</param>
    /// <param name="cbAttribute">The size, in bytes, of the attribute value being set via the pvAttribute parameter.</param>
    /// <returns>If the function succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, int[] pvAttribute, int cbAttribute);
}
