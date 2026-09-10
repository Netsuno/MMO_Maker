using Frog.Core.Events;
using Frog.Editor.Forms.GameData;
using Frog.Editor.Forms.Phase8;
using Frog.Editor.Services;

namespace Frog.Editor.Forms;

/// <summary>Éditeur de pages/conditions/commandes pour un événement catalogue (P8-6).</summary>
internal sealed class MapEventPageEditorDialog : Form
{
    private readonly MapEventsPostgreSqlService _service;
    private readonly Guid _eventId;
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly MapEventPagesEditorPanel _pagesPanel = new() { Dock = DockStyle.Fill };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnClose = new() { Text = "Fermer", AutoSize = true };
    private readonly Label _lblValidation = new() { AutoSize = true, ForeColor = Color.Firebrick, Dock = DockStyle.Top };

    private bool _allowCloseAfterCleanup;
    private bool _cleanupRunning;
    private bool _closeCleanupFailed;
    private Exception? _closeCleanupException;
    private bool _dirty;

    public MapEventPageEditorDialog(MapEventsPostgreSqlService service, Guid eventId, string eventName)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _eventId = eventId;
        Text = $"Pages — {eventName}";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ClientSize = new Size(720, 480);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.RightToLeft,
        };
        bottom.Controls.Add(_btnClose);
        bottom.Controls.Add(_btnPublish);
        bottom.Controls.Add(_btnSave);

        Controls.Add(_pagesPanel);
        Controls.Add(_lblValidation);
        Controls.Add(bottom);

        _pagesPanel.PagesChanged += () => _dirty = true;
        _btnClose.Click += (_, _) => Close();
        _btnSave.Click += (_, _) => _ = _lifecycle.TrackAsync(ct => SaveAsync(publish: false, ct), "save");
        _btnPublish.Click += (_, _) => _ = _lifecycle.TrackAsync(ct => SaveAsync(publish: true, ct), "publish");

        FormClosing += MapEventPageEditorDialog_FormClosing;
        Shown += (_, _) => _ = _lifecycle.RunAsync(LoadPagesAsync, "init");
    }

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal bool IsDirtyForTest => _dirty;

    internal MapEventPagesEditorPanel PagesPanelForTest => _pagesPanel;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal bool CloseCleanupFailedForTest => _closeCleanupFailed;

    internal void RetryCloseCleanupForTest()
    {
        if (_allowCloseAfterCleanup || _cleanupRunning || IsDisposed)
        {
            return;
        }

        _cleanupRunning = true;
        SetClosingUiState(enabled: false);
        _ = RunAsyncCloseCleanupAndMaybeFinishAsync();
    }

    private async Task LoadPagesAsync(CancellationToken ct)
    {
        var pagesJson = await _service.LoadPagesJsonAsync(_eventId).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        if (!MapEventPagesCodec.TryDeserializePages(pagesJson ?? "[]", out var pages, out var error))
        {
            _lblValidation.Text = error ?? "Pages invalides.";
            return;
        }

        _pagesPanel.LoadPages(pages);
        _dirty = false;
        _lblValidation.Text = string.Empty;
    }

    private async Task SaveAsync(bool publish, CancellationToken ct)
    {
        if (!_pagesPanel.TryBuildPages(out var pages, out var buildError))
        {
            _lblValidation.Text = buildError ?? "Pages invalides.";
            return;
        }

        var pagesJson = MapEventPagesCodec.SerializePages(pages);
        if (!MapEventPagesCodec.TryDeserializePages(pagesJson, out _, out var error))
        {
            _lblValidation.Text = error ?? "JSON pages invalide.";
            return;
        }

        var ok = await _service.TrySavePagesAsync(_eventId, pagesJson, publish).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        if (!ok)
        {
            _lblValidation.Text = "Enregistrement échoué.";
            return;
        }

        _dirty = false;
        _lblValidation.Text = string.Empty;
        GameDataUiMessageBox.Show(
            this,
            publish ? "Événement publié." : "Brouillon enregistré.",
            "OK",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void MapEventPageEditorDialog_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowCloseAfterCleanup)
        {
            return;
        }

        e.Cancel = true;
        if (_cleanupRunning)
        {
            return;
        }

        _cleanupRunning = true;
        SetClosingUiState(enabled: false);
        _ = RunAsyncCloseCleanupAndMaybeFinishAsync();
    }

    private async Task RunAsyncCloseCleanupAndMaybeFinishAsync()
    {
        var timeout = EditorTestHooks.GameDataCloseCleanupTimeoutForTest ?? TimeSpan.FromSeconds(30);
        try
        {
            var success = await RunCloseCleanupAsync(timeout).ConfigureAwait(true);
            if (!success)
            {
                _closeCleanupFailed = true;
                _cleanupRunning = false;
                SetClosingUiState(enabled: true);
                if (!IsDisposed)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (IsDisposed)
                        {
                            return;
                        }

                        GameDataUiMessageBox.Show(
                            this,
                            "La fermeture a expiré : une opération est encore en cours.",
                            "Pages événement",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }));
                }

                return;
            }

            _lifecycle.Dispose();
            _closeCleanupFailed = false;
            _allowCloseAfterCleanup = true;
            _cleanupRunning = false;
            if (!IsDisposed)
            {
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed)
                    {
                        Close();
                    }
                }));
            }
        }
        catch (Exception ex)
        {
            _closeCleanupException = ex;
            _closeCleanupFailed = true;
            _cleanupRunning = false;
            SetClosingUiState(enabled: true);
            if (!IsDisposed)
            {
                var message = $"Échec du nettoyage à la fermeture : {ex.Message}";
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed)
                    {
                        return;
                    }

                    GameDataUiMessageBox.Show(
                        this,
                        message,
                        "Pages événement",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }));
            }
        }
    }

    private async Task<bool> RunCloseCleanupAsync(TimeSpan timeout)
    {
        _lifecycle.BeginClosing();
        var drained = await _lifecycle.DrainAsync(timeout).ConfigureAwait(true);
        return drained && _lifecycle.IsIdle;
    }

    private void SetClosingUiState(bool enabled)
    {
        _pagesPanel.Enabled = enabled;
        _btnSave.Enabled = enabled;
        _btnPublish.Enabled = enabled;
        _btnClose.Enabled = enabled;
    }
}
