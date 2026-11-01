#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ShareX.PhotoEditor.Integration;
using ShareX.PhotoEditor.Tools;
using ShareX.PhotoEditor.UI.Commands;
using ShareX.PhotoEditor.UI.Dialogs;
using ShareX.PhotoEditor.UI.Panels;

namespace ShareX.PhotoEditor.UI;

public sealed partial class PhotoEditorWindow
{
    public async Task<IReadOnlyList<string>> PickOpenFilesAsync(string title, bool multiple = true)
    {
        IStorageFolder? start = Services.DefaultFolder != null ? await StorageProvider.TryGetFolderFromPathAsync(Services.DefaultFolder) : null;
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = multiple,
            SuggestedStartLocation = start,
            FileTypeFilter =
            [
                new FilePickerFileType("All supported images") { Patterns = ImageIO.Formats.SelectMany(f => f.Extensions).Select(x => "*" + x).ToArray() },
                .. ImageIO.Formats.Select(f => new FilePickerFileType(f.Name) { Patterns = f.Extensions.Select(x => "*" + x).ToArray() }),
                FilePickerFileTypes.All
            ]
        });
        return files.Select(f => f.TryGetLocalPath()).Where(p => p != null).Select(p => p!).ToList();
    }

    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, bool projectFirst)
    {
        IStorageFolder? start = Document?.FilePath != null ? await StorageProvider.TryGetFolderFromPathAsync(Path.GetDirectoryName(Document.FilePath)!) :
            Services.DefaultFolder != null ? await StorageProvider.TryGetFolderFromPathAsync(Services.DefaultFolder) : null;
        IEnumerable<FileFormatInfo> formats = ImageIO.Formats.Where(f => f.CanSave);
        if (!projectFirst) formats = formats.OrderBy(f => f.Extensions[0] == ImageIO.ProjectExtension);
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            SuggestedStartLocation = start,
            DefaultExtension = projectFirst ? ImageIO.ProjectExtension : ".png",
            ShowOverwritePrompt = true,
            FileTypeChoices = formats.Select(f => new FilePickerFileType(f.Name) { Patterns = f.Extensions.Select(x => "*" + x).ToArray() }).ToList()
        });
        return file?.TryGetLocalPath();
    }

    // ------------------------------------------------------------------ closing

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (IsBusy)
        {
            e.Cancel = true;
            return;
        }
        if (_closingConfirmed)
        {
            SaveSettings();
            return;
        }
        if (_views.Any(v => v.Document.IsDirty))
        {
            e.Cancel = true;
            foreach (DocumentView view in _views.ToList())
            {
                if (!await CloseViewAsync(view)) return;
            }
            _closingConfirmed = true;
            Close();
            return;
        }
        SaveSettings();
        foreach (DocumentView view in _views.ToList())
        {
            view.Dispose();
            view.Document.Dispose();
        }
        _views.Clear();
    }

    public void SaveSettings()
    {
        _dock.SaveLayout();
        if (_dockColumn.ActualWidth > 0) Settings.RightDockWidth = _dockColumn.ActualWidth;
        Settings.Save(Services.SettingsFilePath);
    }
}
