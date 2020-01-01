﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UniInject;
using UniRx;
using System.Text;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class EditorNoteContextMenuHandler : AbstractContextMenuHandler, INeedInjection
{
    [Inject]
    SongEditorSceneController songEditorSceneController;

    [Inject]
    SongEditorLayerManager layerManager;

    [Inject]
    SongEditorSelectionController selectionController;

    private EditorUiNote uiNote;

    protected override void FillContextMenu(ContextMenu contextMenu)
    {
        if (uiNote == null)
        {
            uiNote = GetComponent<EditorUiNote>();
        }
        if (!selectionController.IsSelected(uiNote.Note))
        {
            selectionController.SetSelection(new List<EditorUiNote> { uiNote });
        }

        contextMenu.AddItem("Split Notes", () => OnSplitNotes());
        contextMenu.AddItem("Merge Notes", () => OnMergeNotes());

        FillContextMenuSetNoteType(contextMenu);
        FillContextMenuMergeSentences(contextMenu);
        FillContextMenuToMoveToOtherSentence(contextMenu);
        FillContextMenuToMoveToOtherVoice(contextMenu);

        contextMenu.AddSeparator();
        contextMenu.AddItem("Delete", () => OnDelete());
    }

    private void FillContextMenuSetNoteType(ContextMenu contextMenu)
    {
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        bool canMakeGolden = selectedNotes.AnyMatch(note => note.Type != ENoteType.Golden);
        bool canMakeFreestyle = selectedNotes.AnyMatch(note => note.Type != ENoteType.Freestyle);
        bool canMakeNormal = selectedNotes.AnyMatch(note => note.Type != ENoteType.Normal);

        contextMenu.AddSeparator();
        if (canMakeGolden)
        {
            contextMenu.AddItem("Make golden", () => OnSetNoteType(ENoteType.Golden));
        }
        if (canMakeFreestyle)
        {
            contextMenu.AddItem("Make freestyle", () => OnSetNoteType(ENoteType.Freestyle));
        }
        if (canMakeNormal)
        {
            contextMenu.AddItem("Make normal", () => OnSetNoteType(ENoteType.Normal));
        }
    }

    private void OnSetNoteType(ENoteType type)
    {
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        foreach (Note note in selectedNotes)
        {
            note.SetType(type);
        }
        songEditorSceneController.OnNotesChanged();
    }

    private void FillContextMenuMergeSentences(ContextMenu contextMenu)
    {
        bool canMergeSentences = CanMergeSentences();
        if (canMergeSentences)
        {
            contextMenu.AddSeparator();
            contextMenu.AddItem("Merge sentences", () => OnMergeSentences());
        }
    }

    private void OnMergeSentences()
    {
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        List<Sentence> oldSentences = selectedNotes.Select(note => note.Sentence).Distinct().ToList();
        Sentence targetSentence = selectedNotes.FirstOrDefault().Sentence;
        foreach (Note note in selectedNotes)
        {
            note.SetSentence(targetSentence);
        }

        // Remove old and now unused sentences.
        foreach (Sentence oldSentence in oldSentences)
        {
            if (oldSentence.Notes.Count == 0)
            {
                oldSentence.SetVoice(null);
            }
            else
            {
                oldSentence.FitToNotes();
            }
        }

        songEditorSceneController.OnNotesChanged();
    }

    private bool CanMergeSentences()
    {
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        return selectedNotes.Count > 1 && selectedNotes.Select(it => it.Sentence).Distinct().Count() > 1;
    }

    private void FillContextMenuToMoveToOtherVoice(ContextMenu contextMenu)
    {
        bool canMoveToVoice1 = CanMoveToVoice(0);
        bool canMoveToVoice2 = CanMoveToVoice(1);
        if (canMoveToVoice1)
        {
            contextMenu.AddSeparator();
            contextMenu.AddItem("Move to player 1", () => OnMoveToVoice(0));
        }
        if (!canMoveToVoice1 && canMoveToVoice2)
        {
            contextMenu.AddSeparator();
        }
        if (canMoveToVoice2)
        {
            contextMenu.AddItem("Move to player 2", () => OnMoveToVoice(1));
        }
    }

    private bool CanMoveToVoice(int index)
    {
        Voice voice = songEditorSceneController.GetOrCreateVoice(index);
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        return selectedNotes.AnyMatch(note => note.Sentence == null || note.Sentence.Voice != voice);
    }

    private void OnMoveToVoice(int index)
    {
        Voice voice = songEditorSceneController.GetOrCreateVoice(index);
        List<Note> selectedNotes = selectionController.GetSelectedNotes();

        List<Sentence> changedSentences = new List<Sentence>();

        foreach (Note note in selectedNotes)
        {
            Sentence lastSentence = note.Sentence;
            // Find a sentence in the new voice for the note
            Sentence sentenceForNote = songEditorSceneController.GetSentenceForNote(note, voice);
            if (sentenceForNote == null)
            {
                // Create new sentence in the voice.
                // Use the min and max value from the sentence of the original note if possible.
                if (note.Sentence != null)
                {
                    sentenceForNote = new Sentence(note.Sentence.MinBeat, note.Sentence.MaxBeat);
                }
                else
                {
                    sentenceForNote = new Sentence();
                }
                sentenceForNote.SetVoice(voice);
                sentenceForNote.AddNote(note);
            }
            else
            {
                note.SetSentence(sentenceForNote);
            }

            changedSentences.Add(sentenceForNote);
            if (lastSentence != null)
            {
                // Remove old sentence if empty now
                if (lastSentence.Notes.Count == 0 && lastSentence.Voice != null)
                {
                    lastSentence.SetVoice(null);
                }
                else
                {
                    changedSentences.Add(lastSentence);
                }
            }

            layerManager.RemoveNoteFromAllLayers(note);
        }

        // Fit changed sentences to their notes (make them as small as possible)
        foreach (Sentence sentence in changedSentences)
        {
            sentence.UpdateMinAndMaxBeat();
            sentence.SetLinebreakBeat(sentence.MaxBeat);
        }

        songEditorSceneController.OnNotesChanged();
    }

    private void FillContextMenuToMoveToOtherSentence(ContextMenu contextMenu)
    {
        bool canMoveToPreviousSentence = CanMoveToPreviousSentence();
        bool canMoveToNextSentence = CanMoveToNextSentence();
        if (canMoveToPreviousSentence)
        {
            contextMenu.AddSeparator();
            contextMenu.AddItem("Move to previous sentence", () => OnMoveToPreviousSentence());
        }
        if (!canMoveToPreviousSentence && canMoveToNextSentence)
        {
            contextMenu.AddSeparator();
        }
        if (canMoveToNextSentence)
        {
            contextMenu.AddItem("Move to next sentence", () => OnMoveToNextSentence());
        }
    }

    private void OnMoveToNextSentence()
    {
        Sentence oldSentence = uiNote.Note.Sentence;

        Sentence nextSentence = songEditorSceneController.GetNextSentence(uiNote.Note.Sentence);
        uiNote.Note.SetSentence(nextSentence);

        // Remove old sentence if not more notes left
        if (oldSentence.Notes.Count == 0)
        {
            oldSentence.SetVoice(null);
        }
        else
        {
            oldSentence.FitToNotes();
        }

        songEditorSceneController.OnNotesChanged();
    }

    private void OnMoveToPreviousSentence()
    {
        Sentence sentenceOld = uiNote.Note.Sentence;

        Sentence previousSentence = songEditorSceneController.GetPreviousSentence(uiNote.Note.Sentence);
        uiNote.Note.SetSentence(previousSentence);
        songEditorSceneController.OnNotesChanged();

        // Remove old sentence if not more notes left
        if (sentenceOld.Notes.Count == 0)
        {
            sentenceOld.SetVoice(null);
        }
    }

    private bool CanMoveToNextSentence()
    {
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        if (selectedNotes.Count != 1)
        {
            return false;
        }

        Note selectedNote = selectedNotes[0];
        if (selectedNote != uiNote.Note || selectedNote.Sentence == null)
        {
            return false;
        }

        // Check that the selected note is the last note in the sentence.
        List<Note> notesInSentence = new List<Note>(selectedNote.Sentence.Notes);
        notesInSentence.Sort(Note.comparerByStartBeat);
        if (notesInSentence.Last() != selectedNote)
        {
            return false;
        }

        // Check that there exists a following sentence
        Sentence nextSentence = songEditorSceneController.GetNextSentence(selectedNote.Sentence);
        return (nextSentence != null);
    }

    private bool CanMoveToPreviousSentence()
    {
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        if (selectedNotes.Count != 1)
        {
            return false;
        }

        Note selectedNote = selectedNotes[0];
        if (selectedNote != uiNote.Note || selectedNote.Sentence == null)
        {
            return false;
        }

        // Check that the selected note is the first note in the sentence.
        List<Note> notesInSentence = new List<Note>(selectedNote.Sentence.Notes);
        notesInSentence.Sort(Note.comparerByStartBeat);
        if (notesInSentence.First() != selectedNote)
        {
            return false;
        }

        // Check that there exists a following sentence
        Sentence previousSentence = songEditorSceneController.GetPreviousSentence(selectedNote.Sentence);
        return (previousSentence != null);
    }

    private void OnSplitNotes()
    {
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        foreach (Note note in selectedNotes)
        {
            if (note.Length > 1)
            {
                int splitBeat = note.StartBeat + (note.Length / 2);
                Note newNote = new Note(note.Type, splitBeat, note.EndBeat - splitBeat, note.TxtPitch, "~");
                newNote.SetSentence(note.Sentence);
                note.SetEndBeat(splitBeat);
            }
        }
        songEditorSceneController.OnNotesChanged();
    }

    private void OnMergeNotes()
    {
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        selectedNotes.Sort(Note.comparerByStartBeat);
        int minBeat = selectedNotes[0].StartBeat;
        int maxBeat = selectedNotes.Select(it => it.EndBeat).Max();
        StringBuilder stringBuilder = new StringBuilder();
        foreach (Note note in selectedNotes)
        {
            if (stringBuilder.Length == 0 || note.Text != "~")
            {
                stringBuilder.Append(note.Text);
            }
        }
        Note targetNote = uiNote.Note;
        Note mergedNote = new Note(targetNote.Type, minBeat, maxBeat - minBeat, targetNote.TxtPitch, stringBuilder.ToString());
        mergedNote.SetSentence(targetNote.Sentence);

        // Remove old notes
        songEditorSceneController.DeleteNotes(selectedNotes);
        songEditorSceneController.OnNotesChanged();
    }

    private void OnDelete()
    {
        List<Note> selectedNotes = selectionController.GetSelectedNotes();
        songEditorSceneController.DeleteNotes(selectedNotes);
        songEditorSceneController.OnNotesChanged();
    }
}
