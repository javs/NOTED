﻿using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using NOTED.Helpers;
using NOTED.Models;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace NOTED.Windows
{
    public class SettingsWindow : Window
    {
        private float _scale => ImGuiHelpers.GlobalScale;
        private Settings Settings => Plugin.Settings;

        public Duty? SelectedDuty = null;
        public Note? SelectedNote = null;

        private bool _addingNote = false;
        private bool _deletingNote = false;
        private uint _newNoteDutyID = 0;
        private string _newNoteDutyName = "";
        private string _newNoteTitle = "";

        private List<TerritoryType> _duties = new List<TerritoryType>();
        private List<TerritoryType> _searchResult = new List<TerritoryType>();

        public SettingsWindow(string name) : base(name)
        {
            ExcelSheet<TerritoryType>? sheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
            if (sheet != null) {
                _duties = sheet.Where(row => row.ContentFinderCondition.Value != null && row.ContentFinderCondition.Value.Name.ToString().Length > 0).ToList();
            }

            Flags = ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoScrollWithMouse;

            Size = new Vector2(740, 600);
        }

        public override void OnClose()
        {
            SelectedDuty = null;
            SelectedNote = null;
        }

        public override void Draw()
        {
            DrawGeneralSettings();
            DrawButtons();
            DrawDutyList();
            DrawNoteList();
            DrawNote();

            if (_addingNote)
            {
                var (didConfirm, didClose) = DrawNewNoteModal();
                if (didConfirm)
                {
                    AddNewNote();
                }

                if (didClose)
                {
                    _addingNote = false;
                }
            }

            if (_deletingNote)
            {
                if (SelectedNote == null)
                {
                    _deletingNote = false;
                    return;
                }

                string[] lines = new string[] { "Are you sure you want the note:", "\"" + SelectedNote.Title + "\"?" };
                var (didConfirm, didClose) = DrawHelper.DrawConfirmationModal("Delete?", lines);
                if (didConfirm && SelectedDuty != null)
                {
                    SelectedDuty.Notes.Remove(SelectedNote);
                    SelectedNote = null;

                    if (SelectedDuty.Notes.Count == 0)
                    {
                        Settings.Duties.Remove(SelectedDuty.ID);
                        SelectedDuty = null;
                    }

                    Settings.Save(Settings);
                }

                if (didClose)
                {
                    _deletingNote = false;
                }
            }
        }

        private void AddNewNote()
        {
            if (!IsNewNoteValid()) { return; }

            Duty? duty = null;
            if (Settings.Duties.TryGetValue(_newNoteDutyID, out Duty? existingDuty))
            {
                duty = existingDuty;
            }

            if (duty == null)
            {
                duty = new Duty(_newNoteDutyID, _newNoteDutyName);
                Settings.Duties.Add(_newNoteDutyID, duty);
            }

            Note newNote = new Note(_newNoteTitle);
            duty.Notes.Add(newNote);

            Settings.Save(Settings);

            SelectedDuty = duty;
            SelectedNote = newNote;
        }

        private void DrawGeneralSettings()
        {
            ImGui.BeginChild("##General", new Vector2(725 * _scale, 37 * _scale), true);
            {
                ImGui.Checkbox("Locked", ref Settings.Locked);
                DrawHelper.SetTooltip("Untick to be able to move and resize the notes.");

                DrawHelper.Tab();
                ImGui.Checkbox("Preview", ref Settings.Preview);
                DrawHelper.SetTooltip("Tick to preview a dummy note and be able to move it and resize it.");

                DrawHelper.Tab();
                ImGui.Checkbox("Right Click to Edit", ref Settings.RightClickToEdit);
                DrawHelper.SetTooltip("When enabled, right clicking on a note will open the configuration window to edit it.");

                DrawHelper.Tab();
                ImGui.ColorEdit4("Locked Backround Color", ref Settings.LockedBackgroundColor, ImGuiColorEditFlags.NoInputs);

                DrawHelper.Tab();
                ImGui.ColorEdit4("Unlocked Backround Color", ref Settings.UnlockedBackgroundColor, ImGuiColorEditFlags.NoInputs);
            }
            ImGui.EndChild();
        }

        private void DrawButtons()
        {
            ImGui.BeginChild("##Buttons", new Vector2(150 * _scale, 37 * _scale), true);
            {
                DrawHelper.Tab(1.1f);
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString()))
                {
                    _newNoteDutyID = 0;
                    _newNoteTitle = "New Note";
                    _addingNote = true;
                }
                ImGui.PopFont();
                DrawHelper.SetTooltip("Adds a new empty note");

                ImGui.SameLine(); DrawHelper.Tab();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button(FontAwesomeIcon.Download.ToIconString()))
                {
                }
                ImGui.PopFont();
                DrawHelper.SetTooltip("Imports a note from the clipboard");
            }
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("##Buttons2", new Vector2(568 * _scale, 37 * _scale), true);
            {
                if (SelectedDuty != null && SelectedNote != null)
                {
                    DrawHelper.Tab(3);
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString()))
                    {
                        _deletingNote = true;
                    }
                    ImGui.PopFont();
                    DrawHelper.SetTooltip("Delete");

                    ImGui.SameLine(); DrawHelper.Tab(5);
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button(FontAwesomeIcon.Upload.ToIconString()))
                    {
                    }
                    ImGui.PopFont();
                    DrawHelper.SetTooltip("Export to the clipboard");

                    int index = SelectedDuty.Notes.IndexOf(SelectedNote);
                    int count = SelectedDuty.Notes.Count;

                    if (count > 1)
                    {
                        string moveHelp = "\nIf you have multiple notes for the same duty that are applicable, the first one will be used.";

                        ImGui.SameLine(); DrawHelper.Tab(5);
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button(FontAwesomeIcon.ArrowUp.ToIconString()))
                        {
                            // circular?
                            if (index == 0)
                            {
                                SelectedDuty.Notes.Remove(SelectedNote);
                                SelectedDuty.Notes.Add(SelectedNote);
                            }
                            else
                            {
                                SelectedDuty.Notes[index] = SelectedDuty.Notes[index - 1];
                                SelectedDuty.Notes[index - 1] = SelectedNote;
                             }
                        }
                        ImGui.PopFont();
                        DrawHelper.SetTooltip("Move Up" + moveHelp);
                    
                        ImGui.SameLine(); DrawHelper.Tab(5);
                        ImGui.PushFont(UiBuilder.IconFont);
                        if (ImGui.Button(FontAwesomeIcon.ArrowDown.ToIconString()))
                        {
                            // circular?
                            if (index == count - 1)
                            {
                                SelectedDuty.Notes.Remove(SelectedNote);
                                SelectedDuty.Notes.Insert(0, SelectedNote);
                            }
                            else
                            {
                                SelectedDuty.Notes[index] = SelectedDuty.Notes[index + 1];
                                SelectedDuty.Notes[index + 1] =SelectedNote;
                            }
                        }
                        ImGui.PopFont();
                        DrawHelper.SetTooltip("Move Down" + moveHelp);
                    }
                }
            }
            ImGui.EndChild();
        }

        private void DrawDutyList()
        {
            ImGui.BeginChild("##DutyList", new Vector2(150 * _scale, 485 * _scale), true);
            {
                foreach (Duty duty in Settings.Duties.Values)
                {
                    if (ImGui.Selectable(duty.Name, duty == SelectedDuty))
                    {
                        SelectedDuty = duty;
                        SelectedNote = duty.Notes.Count > 0 ? duty.Notes[0] : null;
                    }
                }
            }
            ImGui.EndChild();
        }

        private void DrawNoteList()
        {
            ImGui.SameLine();

            ImGui.BeginChild("##NoteList", new Vector2(150 * _scale, 485 * _scale), true);
            {
                if (SelectedDuty != null)
                {
                    for (int i = 0; i < SelectedDuty.Notes.Count; i++)
                    {
                        Note note = SelectedDuty.Notes[i];
                        if (ImGui.Selectable(note.Title + "##note" + i.ToString(), note == SelectedNote))
                        {
                            SelectedNote = note;
                        }
                    }
                }
            }
            ImGui.EndChild();
        }

        private void DrawNote()
        {
            ImGui.SameLine();

            ImGui.BeginChild("##Note", new Vector2(411 * _scale, 485 * _scale), true);
            {
                if (SelectedNote != null) {
                    ImGui.PushItemWidth(398 * _scale);
                    ImGui.InputText("##Title", ref SelectedNote.Title, 64);
                    ImGui.InputTextMultiline("##Text", ref SelectedNote.Text, 99999, new Vector2(398 * _scale, 420 * _scale));

                    ImGui.PushFont(UiBuilder.IconFont);
                    if (ImGui.Button(FontAwesomeIcon.Wrench.ToIconString()))
                    {
                        Plugin.ShowJobsDataWindow(SelectedNote);
                    }
                    ImGui.PopFont();

                    ImGui.SameLine();
                    ImGui.Text("Jobs: " + SelectedNote.Jobs.Text);
                }
            }
            ImGui.EndChild();
        }


        public (bool, bool) DrawNewNoteModal()
        {
            bool didConfirm = false;
            bool didClose = false;
            float width = 300;

            ImGui.OpenPopup("New Note##NOTED");

            Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool p_open = true; // i've no idea what this is used for

            if (ImGui.BeginPopupModal("New Note##NOTED", ref p_open, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.InputText("Title", ref _newNoteTitle, 64);

                ImGui.NewLine();
                if (ImGui.InputText("Duty", ref _newNoteDutyName, 128))
                {
                    _newNoteDutyID = 0;
                    SearchDuty(_newNoteDutyName);
                }

                ImGui.BeginChild("##DutySearch", new Vector2(width * _scale, 170 * _scale), true);
                {
                    List<TerritoryType> list = _newNoteDutyName.Length == 0 ? _duties : _searchResult;

                    foreach (TerritoryType data in list)
                    {
                        string name = UserFriendlyDutyName(data.ContentFinderCondition.Value!.Name.ToString());
                        
                        if (ImGui.Selectable($"{name}", _newNoteDutyID == data.RowId, ImGuiSelectableFlags.None, new Vector2(0, 24)))
                        {
                            _newNoteDutyName = name;
                            _newNoteDutyID = data.RowId;
                        }
                    }
                }
                ImGui.EndChild();

                ImGui.NewLine();
                if (IsNewNoteValid())
                {
                    if (ImGui.Button("OK", new Vector2((width / 2f - 4) * _scale, 24 * _scale)))
                    {
                        ImGui.CloseCurrentPopup();
                        didConfirm = true;
                        didClose = true;
                    }

                    ImGui.SetItemDefaultFocus();
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", new Vector2((width / 2f - 4) * _scale, 24 * _scale)))
                    {
                        ImGui.CloseCurrentPopup();
                        didClose = true;
                    }
                }
                else
                {
                    if (ImGui.Button("Cancel", new Vector2(width * _scale, 24 * _scale)))
                    {
                        ImGui.CloseCurrentPopup();
                        didClose = true;
                    }
                }

                ImGui.EndPopup();
            }
            // close button on nav
            else
            {
                didClose = true;
            }

            return (didConfirm, didClose);
        }

        private void SearchDuty(string text)
        {
            if (text.Length == 0 || _duties.Count == 0)
            {
                _searchResult.Clear();
                return;
            }

            string s = text.ToUpper();
            _searchResult = _duties.Where(row => row.ContentFinderCondition.Value!.Name.ToString().ToUpper().Contains(s)).ToList();
        }

        private bool IsNewNoteValid()
        {
            return _newNoteTitle.Length > 0 && _newNoteDutyName.Length > 0 && _newNoteDutyID != 0;
        }

        private string UserFriendlyDutyName(string name)
        {
            if (name.Length > 1)
            {
                return char.ToUpper(name[0]) + name.Substring(1);
            }

            return name.ToUpper();
        }
    }
}
