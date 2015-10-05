﻿using System;
using System.Collections;
using System.Collections.Generic;
using BansheeEngine;

namespace BansheeEditor
{
    /// <summary>
    /// Base class for objects that display GUI for a modifyable dictionary of elements. Elements can be added, modified or
    /// removed.
    /// </summary>
    public abstract class GUIDictionaryFieldBase
    {
        private const int IndentAmount = 5;

        protected List<GUIDictionaryFieldRow> rows = new List<GUIDictionaryFieldRow>();
        protected GUIDictionaryFieldRow editRow;
        protected GUILayoutX guiChildLayout;
        protected GUILayoutX guiTitleLayout;
        protected GUILayoutY guiContentLayout;
        protected bool isExpanded;
        protected int depth;

        private object editKey;
        private object editValue;
        private object editOriginalKey;

        /// <summary>
        /// Constructs a new GUI dictionary.
        /// </summary>
        protected GUIDictionaryFieldBase()
        { }

        /// <summary>
        /// Updates the GUI dictionary contents. Must be called at least once in order for the contents to be populated.
        /// </summary>
        /// <typeparam name="T">Type of rows that are used to handle GUI for individual dictionary elements.</typeparam>
        /// <param name="title">Label to display on the dictionary GUI title.</param>
        /// <param name="empty">Should the created field represent a null object.</param>
        /// <param name="numRows">Number of rows to create GUI for. Only matters for a non-null dictionary.</param>
        /// <param name="layout">Layout to which to append the list GUI elements to.</param>
        /// <param name="depth">Determines at which depth to render the background. Useful when you have multiple
        ///                     nested containers whose backgrounds are overlaping. Also determines background style,
        ///                     depths divisible by two will use an alternate style.</param>
        protected void Update<T>(LocString title, bool empty, int numRows, GUILayout layout,
            int depth = 0) where T : GUIDictionaryFieldRow, new()
        {
            Destroy();

            this.depth = depth;

            if (empty)
            {
                guiChildLayout = null;
                guiContentLayout = null;
                guiTitleLayout = layout.AddLayoutX();

                guiTitleLayout.AddElement(new GUILabel(title));
                guiTitleLayout.AddElement(new GUILabel("Empty", GUIOption.FixedWidth(100)));

                GUIContent createIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Create));
                GUIButton createBtn = new GUIButton(createIcon, GUIOption.FixedWidth(30));
                createBtn.OnClick += OnCreateButtonClicked;
                guiTitleLayout.AddElement(createBtn);
            }
            else
            {
                GUIToggle guiFoldout = new GUIToggle(title, EditorStyles.Foldout);
                guiFoldout.Value = isExpanded;
                guiFoldout.OnToggled += OnFoldoutToggled;

                GUIContent clearIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Clear));
                GUIButton guiClearBtn = new GUIButton(clearIcon, GUIOption.FixedWidth(30));
                guiClearBtn.OnClick += OnClearButtonClicked;

                GUIContent addIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Add));
                GUIButton guiAddBtn = new GUIButton(addIcon, GUIOption.FixedWidth(30));
                guiAddBtn.OnClick += OnAddButtonClicked;

                guiTitleLayout = layout.AddLayoutX();
                guiTitleLayout.AddElement(guiFoldout);
                guiTitleLayout.AddElement(guiAddBtn);
                guiTitleLayout.AddElement(guiClearBtn);

                if (numRows > 0)
                {
                    guiChildLayout = layout.AddLayoutX();
                    guiChildLayout.AddSpace(IndentAmount);
                    guiChildLayout.Enabled = isExpanded;

                    GUIPanel guiContentPanel = guiChildLayout.AddPanel();
                    GUILayoutX guiIndentLayoutX = guiContentPanel.AddLayoutX();
                    guiIndentLayoutX.AddSpace(IndentAmount);
                    GUILayoutY guiIndentLayoutY = guiIndentLayoutX.AddLayoutY();
                    guiIndentLayoutY.AddSpace(IndentAmount);
                    guiContentLayout = guiIndentLayoutY.AddLayoutY();
                    guiIndentLayoutY.AddSpace(IndentAmount);
                    guiIndentLayoutX.AddSpace(IndentAmount);
                    guiChildLayout.AddSpace(IndentAmount);

                    short backgroundDepth = (short)(Inspector.START_BACKGROUND_DEPTH - depth - 1);
                    string bgPanelStyle = depth % 2 == 0
                        ? EditorStyles.InspectorContentBgAlternate
                        : EditorStyles.InspectorContentBg;

                    GUIPanel backgroundPanel = guiContentPanel.AddPanel(backgroundDepth);
                    GUITexture inspectorContentBg = new GUITexture(null, bgPanelStyle);
                    backgroundPanel.AddElement(inspectorContentBg);

                    for (int i = 0; i < numRows; i++)
                    {
                        GUIDictionaryFieldRow newRow = new T();
                        newRow.BuildGUI(this, guiContentLayout, i, depth);

                        rows.Add(newRow);
                    }

                    editRow = new T();
                    editRow.BuildGUI(this, guiContentLayout, numRows, depth);
                    editRow.Enabled = false;
                }
            }
        }

        /// <summary>
        /// Returns the layout that is used for positioning the elements in the title bar.
        /// </summary>
        /// <returns>Horizontal layout for positioning the title bar elements.</returns>
        public GUILayoutX GetTitleLayout()
        {
            return guiTitleLayout;
        }

        /// <summary>
        /// Refreshes contents of all dictionary rows and checks if anything was modified.
        /// </summary>
        /// <returns>True if any entry in the list was modified, false otherwise.</returns>
        public bool Refresh()
        {
            bool anythingModified = false;

            for (int i = 0; i < rows.Count; i++)
            {
                bool updateGUI;

                anythingModified |= rows[i].Refresh(out updateGUI);

                if (updateGUI)
                    rows[i].BuildGUI(this, guiContentLayout, i, depth);
            }

            if (editRow.Enabled)
            {
                bool updateGUI;
                anythingModified |= editRow.Refresh(out updateGUI);

                if (updateGUI)
                    editRow.BuildGUI(this, guiContentLayout, rows.Count, depth);
            }

            return anythingModified;
        }

        /// <summary>
        /// Destroys the GUI elements.
        /// </summary>
        public void Destroy()
        {
            if (guiTitleLayout != null)
            {
                guiTitleLayout.Destroy();
                guiTitleLayout = null;
            }

            if (guiChildLayout != null)
            {
                guiChildLayout.Destroy();
                guiChildLayout = null;
            }

            for (int i = 0; i < rows.Count; i++)
                rows[i].Destroy();

            rows.Clear();

            editRow.Destroy();
        }

        /// <summary>
        /// Gets a value of an element at the specified index in the list. Also handles temporary edit fields.
        /// </summary>
        /// <param name="key">Key of the element whose value to retrieve.</param>
        /// <returns>Value of the list element at the specified key.</returns>
        protected internal virtual object GetValueInternal(object key)
        {
            if (key == editKey)
                return editValue;

            return GetValue(key);
        }

        /// <summary>
        /// Sets a value of an element at the specified index in the list. Also handles temporary edit fields.
        /// </summary>
        /// <param name="key">Key of the element whose value to set.</param>
        /// <param name="value">Value to assign to the element. Caller must ensure it is of valid type.</param>
        protected internal virtual void SetValueInternal(object key, object value)
        {
            if (key == editKey)
                editValue = value;

            SetValue(key, value);
        }

        /// <summary>
        /// Gets a value of an element at the specified index in the list.
        /// </summary>
        /// <param name="key">Key of the element whose value to retrieve.</param>
        /// <returns>Value of the list element at the specified key.</returns>
        protected internal abstract object GetValue(object key);

        /// <summary>
        /// Sets a value of an element at the specified index in the list.
        /// </summary>
        /// <param name="key">Key of the element whose value to set.</param>
        /// <param name="value">Value to assign to the element. Caller must ensure it is of valid type.</param>
        protected internal abstract void SetValue(object key, object value);

        /// <summary>
        /// Adds a new entry to the dictionary.
        /// </summary>
        /// <param name="key">Key of the entry to add.</param>
        /// <param name="value">Value of the entry to add.</param>
        protected internal abstract void AddEntry(object key, object value);

        /// <summary>
        /// Removes the specified entry from the dictionary.
        /// </summary>
        /// <param name="key">Key of the entry to remove.</param>
        protected internal abstract void RemoveEntry(object key);

        /// <summary>
        /// Creates a new empty key object of a valid type that can be used in the dictionary.
        /// </summary>
        /// <returns>New empty key object.</returns>
        protected internal abstract object CreateKey();

        /// <summary>
        /// Creates a new empty value object of a valid type that can be used in the dictionary.
        /// </summary>
        /// <returns>New empty value object.</returns>
        protected internal abstract object CreateValue();

        /// <summary>
        /// Checks does the element with the specified key exist in the dictionary.
        /// </summary>
        /// <param name="key">Key of the element to check for existence.</param>
        /// <returns>True if the key exists in the dictionary, false otherwise.</returns>
        protected internal abstract bool Contains(object key);

        /// <summary>
        /// Triggered when the user clicks on the expand/collapse toggle in the title bar.
        /// </summary>
        /// <param name="expanded">Determines whether the contents were expanded or collapsed.</param>
        private void OnFoldoutToggled(bool expanded)
        {
            isExpanded = expanded;

            if (guiChildLayout != null)
                guiChildLayout.Enabled = isExpanded;
        }

        /// <summary>
        /// Triggered when the user clicks on the create button on the title bar. Creates a brand new dictionary with zero
        /// elements in the place of the current dictionary.
        /// </summary>
        protected abstract void OnCreateButtonClicked();

        /// <summary>
        /// Triggered when the user clicks on the add button on the title bar. Adds a new empty element to the dictionary.
        /// </summary>
        protected virtual void OnAddButtonClicked()
        {
            if (editKey != null)
            {
                DialogBox.Open(
                    new LocEdString("Edit in progress."),
                    new LocEdString("You are editing the entry with key \"" + editKey + "\". You cannot add a row " + 
                        "before applying or discarding those changes. Do you wish to apply those changes first?"),
                    DialogBox.Type.YesNoCancel,
                    x =>
                    {
                        switch (x)
                        {
                            case DialogBox.ResultType.Yes:
                                if (ApplyChanges())
                                    StartAdd();
                                break;
                            case DialogBox.ResultType.No:
                                DiscardChanges();
                                StartAdd();
                                break;
                        }
                    });
            }
            else
                StartAdd();
        }

        /// <summary>
        /// Triggered when the user clicks on the clear button on the title bar. Deletes the current dictionary object.
        /// </summary>
        protected abstract void OnClearButtonClicked();

        /// <summary>
        /// Triggered when the user clicks on the delete button next to a dictionary entry. Deletes an element in the 
        /// dictionary. 
        /// </summary>
        /// <param name="key">Key of the element to remove.</param>
        protected internal virtual void OnDeleteButtonClicked(object key)
        {
            if (editKey != null)
                DiscardChanges();
            else
                RemoveEntry(key);
        }

        /// <summary>
        /// Triggered when the user clicks on the clone button next to a dictionary entry. Clones an element and
        /// adds the clone to the dictionary.
        /// </summary>
        /// <param name="key">Key of the element to clone.</param>
        protected internal virtual void OnCloneButtonClicked(object key)
        {
            if (editKey != null)
            {
                DialogBox.Open(
                    new LocEdString("Edit in progress."),
                    new LocEdString("You are editing the entry with key \"" + editKey + "\". You cannot clone a row " +
                        "before applying or discarding those changes. Do you wish to apply those changes first?"),
                    DialogBox.Type.YesNoCancel,
                    x =>
                    {
                        switch (x)
                        {
                            case DialogBox.ResultType.Yes:
                                if (ApplyChanges())
                                    StartClone(key);
                                break;
                            case DialogBox.ResultType.No:
                                DiscardChanges();
                                StartClone(key);
                                break;
                        }
                    });
            }
            else
                StartClone(key);
        }

        /// <summary>
        /// Triggered when user clicks the edit or apply (depending on state) button next to the dictionary entry. Starts
        /// edit mode for the element, if not already in edit mode. Applies edit mode changes if already in edit mode.
        /// </summary>
        /// <param name="key">Key of the element to edit.</param>
        protected internal virtual void OnEditButtonClicked(object key)
        {
            if (editKey == key)
                ApplyChanges();
            else
            {
                if (editKey != null)
                {
                    DialogBox.Open(
                        new LocEdString("Edit in progress."),
                        new LocEdString("You are already editing the entry with key \"" + editKey + "\". You cannot edit " +
                            "another row before applying or discarding those changes. Do you wish to apply those changes first?"),
                        DialogBox.Type.YesNoCancel,
                        x =>
                        {
                            switch (x)
                            {
                                case DialogBox.ResultType.Yes:
                                    if (ApplyChanges())
                                        StartEdit(key);
                                    break;
                                case DialogBox.ResultType.No:
                                    DiscardChanges();
                                    StartEdit(key);
                                    break;
                            }
                        });
                }
                else
                    StartEdit(key);
            }
        }

        /// <summary>
        /// Returns a row that displays contents of the entry under the specified key.
        /// </summary>
        /// <param name="key">Key of the row to retrieve.</param>
        /// <returns>GUI representation of the row under the specified key if found, null otherwise.</returns>
        private GUIDictionaryFieldRow GetRow(object key)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Key == key)
                    return rows[i];
            }

            return null;
        }

        /// <summary>
        /// Starts an edit operation on a row with the specified key. Allows the user to change the key of the specified row.
        /// Caller must ensure no edit operation is already in progress.
        /// </summary>
        /// <param name="key">Key of the row to start the edit operation on.</param>
        private void StartEdit(object key)
        {
            editKey = SerializableUtility.Clone(key);
            editValue = SerializableUtility.Clone(GetValue(key));
            editOriginalKey = key;

            GUIDictionaryFieldRow row = GetRow(key);
            row.EditMode = true;
        }

        /// <summary>
        /// Starts an add operation. Adds a new key/value pair and allows the user to set them up in a temporary row
        /// before inserting them into the dictionary. Caller must ensure no edit operation is already in progress.
        /// </summary>
        private void StartAdd()
        {
            editKey = CreateKey();
            editValue = CreateValue();
            editOriginalKey = null;

            editRow.Key = editKey;
            editRow.Enabled = true;
            editRow.EditMode = true;
        }

        /// <summary>
        /// Starts a clone operation. Adds a new key/value pair by cloning an existing one. Allows the user to modify the 
        /// new pair in a temporary row before inserting them into the dictionary. Caller must ensure no edit operation is 
        /// already in progress.
        /// </summary>
        /// <param name="key">Key of the row to clone.</param>
        private void StartClone(object key)
        {
            editKey = SerializableUtility.Clone(key);
            editValue = SerializableUtility.Clone(GetValue(key));
            editOriginalKey = null;

            editRow.Key = editKey;
            editRow.Enabled = true;
            editRow.EditMode = true;
        }

        /// <summary>
        /// Attempts to apply any changes made to the currently edited row.
        /// </summary>
        /// <returns>True if the changes were successfully applied, false if the new key already exists in the dictionary.
        ///          </returns>
        private bool ApplyChanges()
        {
            if (editKey == null)
                return true;

            if (Contains(editKey))
            {
                DialogBox.Open(
                    new LocEdString("Key already exists."),
                    new LocEdString("Cannot add a key \"" + editKey + "\" to dictionary. Key already exists"),
                    DialogBox.Type.OK);

                return false;
            }
            else
            {
                if (editOriginalKey != null)
                {
                    RemoveEntry(editOriginalKey);

                    GUIDictionaryFieldRow row = GetRow(editOriginalKey);
                    row.EditMode = false;
                }
                else // No original key means its a new element which uses the temporary edit row
                {
                    editRow.EditMode = false;
                    editRow.Enabled = false;
                }

                AddEntry(editKey, editValue);
                editKey = null;
                editValue = null;
                editOriginalKey = null;

                return true;
            }
        }

        /// <summary>
        /// Cancels any changes made on the currently edited row.
        /// </summary>
        private void DiscardChanges()
        {
            if (editKey != null)
            {
                editKey = null;
                editValue = null;
                editRow.Enabled = false;
            }
        }
    }

    /// <summary>
    /// Creates GUI elements that allow viewing and manipulation of a <see cref="Dictionary{TKey,TValue}"/>. When constructing the
    /// object user can provide a custom type that manages GUI for individual dictionary elements.
    /// </summary>
    /// <typeparam name="Key">Type of key used by the dictionary.</typeparam>
    /// <typeparam name="Value">Type of value stored in the dictionary.</typeparam>
    public class GUIDictionaryField<Key, Value> : GUIDictionaryFieldBase 
    {
        /// <summary>
        /// Triggered when the reference array has been changed. This does not include changes that only happen to its 
        /// internal elements.
        /// </summary>
        public Action<Dictionary<Key, Value>> OnChanged;

        /// <summary>
        /// Triggered when an element in the list has been changed.
        /// </summary>
        public Action<Key> OnValueChanged;

        /// <summary>
        /// Array object whose contents are displayed.
        /// </summary>
        public Dictionary<Key, Value> Dictionary { get { return dictionary; } }
        protected Dictionary<Key, Value> dictionary;

        /// <summary>
        /// Constructs a new empty dictionary GUI.
        /// </summary>
        public GUIDictionaryField()
        { }

        /// <summary>
        /// Updates the GUI dictionary contents. Must be called at least once in order for the contents to be populated.
        /// </summary>
        /// <typeparam name="RowType">Type of rows that are used to handle GUI for individual dictionary elements.</typeparam>
        /// <param name="title">Label to display on the list GUI title.</param>
        /// <param name="dictionary">Object containing the data. Can be null.</param>
        /// <param name="layout">Layout to which to append the list GUI elements to.</param>
        /// <param name="depth">Determines at which depth to render the background. Useful when you have multiple
        ///                     nested containers whose backgrounds are overlaping. Also determines background style,
        ///                     depths divisible by two will use an alternate style.</param>
        public void Update<RowType>(LocString title, Dictionary<Key, Value> dictionary, 
            GUILayout layout, int depth = 0)
            where RowType : GUIDictionaryFieldRow, new()
        {
            this.dictionary = dictionary;

            if (dictionary != null)
                base.Update<RowType>(title, false, dictionary.Count, layout, depth);
            else
                base.Update<RowType>(title, true, 0, layout, depth);
        }

        /// <inheritdoc/>
        protected internal override object GetValue(object key)
        {
            return dictionary[(Key)key];
        }

        /// <inheritdoc/>
        protected internal override void SetValue(object key, object value)
        {
            dictionary[(Key)key] = (Value)value;

            if (OnValueChanged != null)
                OnValueChanged((Key)key);
        }

        /// <inheritdoc/>
        protected internal override bool Contains(object key)
        {
            return dictionary.ContainsKey((Key)key); ;
        }

        /// <inheritdoc/>
        protected internal override void AddEntry(object key, object value)
        {
            dictionary[(Key)key] = (Value)value;

            if (OnChanged != null)
                OnChanged(dictionary);
        }

        /// <inheritdoc/>
        protected internal override void RemoveEntry(object key)
        {
            dictionary.Remove((Key) key);

            if (OnChanged != null)
                OnChanged(dictionary);
        }

        /// <inheritdoc/>
        protected internal override object CreateKey()
        {
            return default(Key);
        }

        /// <inheritdoc/>
        protected internal override object CreateValue()
        {
            return default(Value);
        }

        /// <inheritdoc/>
        protected override void OnCreateButtonClicked()
        {
            dictionary = new Dictionary<Key, Value>();

            if (OnChanged != null)
                OnChanged(dictionary);
        }

        /// <inheritdoc/>
        protected override void OnClearButtonClicked()
        {
            dictionary = null;

            if (OnChanged != null)
                OnChanged(dictionary);
        }
    }

    /// <summary>
    /// Contains GUI elements for a single entry in a dictionary.
    /// </summary>
    public abstract class GUIDictionaryFieldRow
    {
        private GUILayoutY rowLayout;
        private GUILayoutX keyRowLayout;
        private GUILayoutY keyLayout;
        private GUILayoutY valueLayout;
        private GUILayoutX titleLayout;
        private GUIButton deleteBtn;
        private GUIButton editBtn;
        private bool localTitleLayout;
        private bool enabled = true;
        private GUIDictionaryFieldBase parent;

        protected object key;
        protected int depth;

        /// <summary>
        /// Key of the dictionary entry displayed by this row GUI.
        /// </summary>
        internal object Key
        {
            get { return key; }
            set
            {
                if(rowLayout != null)
                {
                    rowLayout.Clear();
                    rowLayout = null;
                    keyRowLayout = null;
                    keyLayout = null;
                    valueLayout = null;
                    titleLayout = null;
                    localTitleLayout = false;
                }

                BuildGUI(parent, null, value, depth);
            }
        }

        /// <summary>
        /// Determines is the row currently being displayed.
        /// </summary>
        internal bool Enabled
        {
            get { return enabled; }
            set { enabled = value; rowLayout.Enabled = value; }
        }

        /// <summary>
        /// Enables or disables the row's edit mode. This determines what type of buttons are shown on the row title bar.
        /// </summary>
        internal bool EditMode
        {
            set
            {
                if (value)
                {
                    GUIContent cancelIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Cancel));
                    GUIContent applyIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Apply));

                    deleteBtn.SetContent(cancelIcon);
                    editBtn.SetContent(applyIcon);
                }
                else
                {
                    GUIContent deleteIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Delete));
                    GUIContent editIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Edit));

                    deleteBtn.SetContent(deleteIcon);
                    editBtn.SetContent(editIcon);
                }
            }
        }

        /// <summary>
        /// Constructs a new dictionary row object.
        /// </summary>
        protected GUIDictionaryFieldRow()
        {

        }

        /// <summary>
        /// (Re)creates all row GUI elements.
        /// </summary>
        /// <param name="parent">Parent array GUI object that the entry is contained in.</param>
        /// <param name="parentLayout">Parent layout that row GUI elements will be added to.</param>
        /// <param name="key">Key of the element to create GUI for.</param>
        /// <param name="depth">Determines the depth at which the element is rendered.</param>
        internal void BuildGUI(GUIDictionaryFieldBase parent, GUILayout parentLayout, object key, int depth)
        {
            this.parent = parent;
            this.key = key;
            this.depth = depth;

            if (rowLayout == null)
                rowLayout = parentLayout.AddLayoutY();

            if (keyRowLayout == null)
                keyRowLayout = rowLayout.AddLayoutX();

            if (keyLayout == null)
                keyLayout = keyRowLayout.AddLayoutY();

            if (valueLayout == null)
                valueLayout = rowLayout.AddLayoutY();

            GUILayoutX externalTitleLayout = CreateKeyGUI(keyLayout);
            CreateValueGUI(valueLayout);
            if (localTitleLayout || (titleLayout != null && titleLayout == externalTitleLayout))
                return;

            if (externalTitleLayout != null)
            {
                localTitleLayout = false;
                titleLayout = externalTitleLayout;
            }
            else
            {
                GUILayoutY buttonCenter = keyRowLayout.AddLayoutY();
                buttonCenter.AddFlexibleSpace();
                titleLayout = buttonCenter.AddLayoutX();
                buttonCenter.AddFlexibleSpace();

                localTitleLayout = true;
            }

            GUIContent cloneIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Clone));
            GUIContent deleteIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Delete));
            GUIContent editIcon = new GUIContent(EditorBuiltin.GetInspectorWindowIcon(InspectorWindowIcon.Edit));

            GUIButton cloneBtn = new GUIButton(cloneIcon, GUIOption.FixedWidth(30));
            deleteBtn = new GUIButton(deleteIcon, GUIOption.FixedWidth(30));
            editBtn = new GUIButton(editIcon, GUIOption.FixedWidth(30));

            cloneBtn.OnClick += () => parent.OnCloneButtonClicked(key);
            deleteBtn.OnClick += () => parent.OnDeleteButtonClicked(key);
            editBtn.OnClick += () => parent.OnEditButtonClicked(key);

            titleLayout.AddElement(cloneBtn);
            titleLayout.AddElement(deleteBtn);
            titleLayout.AddSpace(10);
            titleLayout.AddElement(editBtn);
        }

        /// <summary>
        /// Creates GUI elements specific to type in the key portion of a dictionary entry.
        /// </summary>
        /// <param name="layout">Layout to insert the row GUI elements to.</param>
        /// <returns>An optional title bar layout that the standard dictionary buttons will be appended to.</returns>
        protected abstract GUILayoutX CreateKeyGUI(GUILayoutY layout);

        /// <summary>
        /// Creates GUI elements specific to type in the key portion of a dictionary entry.
        /// </summary>
        /// <param name="layout">Layout to insert the row GUI elements to.</param>
        protected abstract void CreateValueGUI(GUILayoutY layout);

        /// <summary>
        /// Refreshes the GUI for the dictionary row and checks if anything was modified.
        /// </summary>
        /// <param name="rebuildGUI">Determines should the field's GUI elements be updated due to modifications.</param>
        /// <returns>True if any modifications were made, false otherwise.</returns>
        internal protected virtual bool Refresh(out bool rebuildGUI)
        {
            rebuildGUI = false;
            return false;
        }

        /// <summary>
        /// Gets the value contained in this dictionary's row.
        /// </summary>
        /// <typeparam name="T">Type of the value. Must match the dictionary's element type.</typeparam>
        /// <returns>Value in this dictionary's row.</returns>
        protected T GetValue<T>()
        {
            return (T)parent.GetValueInternal(key);
        }

        /// <summary>
        /// Sets the value contained in this dictionary's row.
        /// </summary>
        /// <typeparam name="T">Type of the value. Must match the dictionary's element type.</typeparam>
        /// <param name="value">Value to set.</param>
        protected void SetValue<T>(T value)
        {
            parent.SetValueInternal(key, value);
        }

        /// <summary>
        /// Destroys all row GUI elements.
        /// </summary>
        public void Destroy()
        {
            rowLayout.Destroy();
            rowLayout = null;
        }
    }
}