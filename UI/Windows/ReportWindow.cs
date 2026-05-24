namespace Glance.UI.Windows;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Glance.Core;
using Glance.Utils;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Numerics;
using System.Text;
using System.Text.Json;

public sealed class ReportWindow : Window
{
    static string? _name, _world, _profileId;
    static int _selectedReason = -1;
    static string _notes = "";
    static bool _sending, _sent, _error;

    static readonly (string Id, string Label, FontAwesomeIcon Icon)[] Reasons = [
        ("inappropriate", "Inappropriate / NSFW Content", FontAwesomeIcon.ExclamationTriangle),
        ("copyright", "Copyright Infringement", FontAwesomeIcon.Copyright),
        ("harassment", "Targeted Harassment", FontAwesomeIcon.Crosshairs),
        ("ai", "AI-Generated Content", FontAwesomeIcon.Robot),
        ("irrelevant", "Irrelevant Content", FontAwesomeIcon.FileExcel),
        ("plagiarism", "Plagiarism", FontAwesomeIcon.Copy),
    ];

    public ReportWindow() : base("Report Profile##Report",
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize)
    {
        Size = new Vector2(400 * ImGuiHelpers.GlobalScale, 0);
        SizeCondition = ImGuiCond.Always;
    }

    public static void Open(string? name, string? world, string? profileId)
    {
        _name = name;
        _world = world;
        _profileId = profileId;
        _selectedReason = -1;
        _notes = "";
        _sending = false;
        _sent = false;
        _error = false;
        Globals.ReportWindow.IsOpen = true;
    }

    public override void Draw()
    {
        if (_sent)
        {
            UI.Space(UI.Md);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var checkSize = ImGui.CalcTextSize(FontAwesomeIcon.CheckCircle.ToIconString());
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - checkSize.X) / 2);
                ImGui.TextColored(new Vector4(0.4f, 0.8f, 0.4f, 1f), FontAwesomeIcon.CheckCircle.ToIconString());
            }

            UI.Space(UI.Sm);
            Theme.Centered("Report Submitted", new Vector4(0.9f, 0.9f, 0.9f, 1f));
            Theme.Centered("Our team will review within 24-48 hours.", Theme.TextMuted);
            UI.Space(UI.Md);

            var bw = 120f * ImGuiHelpers.GlobalScale;
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - bw) / 2);
            if (ImGui.Button("Close", new Vector2(bw, 28 * ImGuiHelpers.GlobalScale)))
                IsOpen = false;
            return;
        }

        ImGui.TextColored(Theme.LabelColor, "Reporting:");
        ImGui.SameLine();
        ImGui.TextColored(Theme.ValueColor, $"{_name} @ {_world}");
        UI.Space(UI.Sm);
        ImGui.Separator();
        UI.Space(UI.Sm);

        ImGui.TextColored(Theme.LabelColor, "Reason");
        UI.Space(UI.Xs);

        for (var i = 0; i < Reasons.Length; i++)
        {
            var (id, label, icon) = Reasons[i];
            var selected = _selectedReason == i;

            using (ImRaii.PushColor(ImGuiCol.Button, selected ? Theme.Error with { W = 0.25f } : Theme.ButtonBg)
                .Push(ImGuiCol.ButtonHovered, selected ? Theme.Error with { W = 0.35f } : Theme.ButtonHovered)
                .Push(ImGuiCol.Border, selected ? Theme.Error with { W = 0.6f } : Theme.FrameBorder))
            using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, selected ? 1f : 0f))
            {
                if (ImGui.Button($"##reason{i}", new Vector2(ImGui.GetContentRegionAvail().X, 32 * ImGuiHelpers.GlobalScale)))
                {
                    _selectedReason = i;
                    Sound.PlayClick();
                }
            }

            var min = ImGui.GetItemRectMin();
            var dl = ImGui.GetWindowDrawList();

            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var iconStr = icon.ToIconString();
                dl.AddText(min + new Vector2(10, 8) * ImGuiHelpers.GlobalScale, Theme.Col(selected ? Theme.Error : Theme.LabelColorDim), iconStr);
            }

            dl.AddText(min + new Vector2(32, 8) * ImGuiHelpers.GlobalScale, Theme.Col(selected ? new Vector4(1, 1, 1, 1) : Theme.LabelColor), label);

            if (selected)
            {
                var buttonWidth = ImGui.GetItemRectSize().X;
                var dot = new Vector2(min.X + buttonWidth - 20 * ImGuiHelpers.GlobalScale, min.Y + 16 * ImGuiHelpers.GlobalScale);
                dl.AddCircleFilled(dot, 4, Theme.Col(Theme.Error));
            }

            UI.Space(2 * ImGuiHelpers.GlobalScale);
        }

        UI.Space(UI.Sm);

        ImGui.TextColored(Theme.LabelColor, "Additional notes");
        ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, "(optional)");
        UI.Space(UI.Xs);

        using (ImRaii.PushColor(ImGuiCol.FrameBg, Theme.ButtonBg with { W = 0.5f }))
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(UI.Sm)))
        {
            ImGui.InputTextMultiline("##notes", ref _notes, 500, new Vector2(ImGui.GetContentRegionAvail().X, 60 * ImGuiHelpers.GlobalScale));
        }

        ImGui.TextColored(Theme.TextMuted, $"{_notes.Length}/500");

        if (_error)
        {
            UI.Space(UI.Xs);
            ImGui.TextColored(Theme.Error, "Failed to submit. Try again later.");
        }

        UI.Space(UI.Sm);

        var canSubmit = !_sending && _selectedReason >= 0;
        using (ImRaii.Disabled(!canSubmit))
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.Error with { W = 0.7f })
            .Push(ImGuiCol.ButtonHovered, Theme.Error with { W = 0.9f }))
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1, 1, 1, 1)))
        {
            if (ImGui.Button(_sending ? "Submitting..." : "Submit Report", new Vector2(150, 28) * ImGuiHelpers.GlobalScale))
                Submit();
        }

        ImGui.SameLine(0, UI.Sm);
        if (ImGui.Button("Cancel", new Vector2(100, 28) * ImGuiHelpers.GlobalScale))
            IsOpen = false;

        UI.Space(UI.Xs);
        Theme.Centered("Reports are reviewed within 24-48 hours", Theme.TextMuted);
    }

    static async void Submit()
    {
        if (_sending) return;
        _sending = true;
        _error = false;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://beacon.rphub.co/report")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", Globals.Auth.CurrentJwt) },
                Content = JsonContent.Create(new
                {
                    profileId = _profileId,
                    name = _name,
                    world = _world,
                    reason = Reasons[_selectedReason].Id,
                    notes = _notes.Trim(),
                })
            };

            var res = await Globals.Http.SendAsync(req);
            _sent = res.IsSuccessStatusCode;
            if (!_sent)
            {
                var body = await res.Content.ReadAsStringAsync();
                Globals.Log.Error($"[Report] {res.StatusCode}: {body}");
            }
            _error = !_sent;
        }
        catch
        {
            _error = true;
        }
        finally
        {
            _sending = false;
        }
    }
}
