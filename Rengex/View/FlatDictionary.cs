using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace Rengex.View {
  /// <summary>
  /// A utility for importing separate xaml files while previewing it in xaml editor.
  /// </summary>
  // <example>
  // <ResourceDictionary>
  //     <ResourceDictionary.MergedDictionaries>
  //       <local:FlatDictionary Key = "Import" DesignSource="pack://application,,,/res/Import.xaml"/>
  //     </ResourceDictionary.MergedDictionaries>
  //  </ResourceDictionary>
  // </Window.Resources>
  internal class FlatDictionary : ResourceDictionary, ISupportInitialize {

    // TODO: It seems to be able to support autocomplete by EndInit() and
    // LoadComponent(object, Uri), but have limitation from
    // Application.LoadComponent that cannot reference other assembly.

    private class Proxy { }

    private string? _Key;
    public string? Key {
      get { return _Key; }
      set {
        _Key = value;
        if (DesignSource != null) {
          Register();
        }
      }
    }

    private Uri? _DesignSource;
    public Uri? DesignSource {
      get => _DesignSource;
      set {
        _DesignSource = value;
        if (Key != null) {
          Register();
        }
      }
    }

    private void Register() {
      this[Key] = new Proxy();
    }

    protected override void OnGettingValue(object key, ref object? value, out bool canCache) {
      if (value is Proxy) {
        value = LoadComponent(DesignSource);
        canCache = false;
      }
      else {
        base.OnGettingValue(key, ref value, out canCache);
      }
    }

    private static object? LoadComponent(Uri? uri) {
      if (uri == null) {
        return null;
      }

      if (uri.OriginalString.Length >= 2 && uri.OriginalString[1] == ':') {
        using var stream = new FileStream(uri.OriginalString, FileMode.Open);
        return XamlReader.Load(stream);
      }

      Uri relUri;
      if (uri.OriginalString.StartsWith("pack:", StringComparison.Ordinal)) {
        relUri = new Uri($";component{uri.AbsolutePath}", UriKind.Relative);
      }
      else {
        relUri = uri;
      }
      return Application.LoadComponent(relUri);
    }
  }
}
