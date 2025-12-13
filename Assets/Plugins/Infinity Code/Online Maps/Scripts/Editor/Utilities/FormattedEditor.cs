/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace OnlineMaps.Editors
{
    public abstract class FormattedEditor : Editor
    {
        protected LayoutItem rootLayoutItem;
        protected virtual float minLabelWidth => 170;

        protected abstract void CacheSerializedFields();

        protected virtual void GenerateLayoutItems()
        {
            rootLayoutItem = new LayoutItem("root");
        }

        protected virtual void OnDisable()
        {
            rootLayoutItem?.Dispose();
            rootLayoutItem = null;
        }

        protected virtual void OnEnable()
        {
            if (!target) return;
            
            try
            {
                serializedObject.Update();

                OnEnableBefore();
                CacheSerializedFields();
                GenerateLayoutItems();
                OnEnableLate();

                serializedObject.ApplyModifiedProperties();

                rootLayoutItem.Sort();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        protected virtual void OnEnableBefore()
        {
        
        }

        protected virtual void OnEnableLate()
        {
        
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            float labelWidth = EditorGUIUtility.labelWidth;
            if (labelWidth < minLabelWidth) EditorGUIUtility.labelWidth = minLabelWidth;

            try
            {
                EditorGUI.BeginChangeCheck();

                rootLayoutItem.Draw();

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();

                    OnSetDirty();
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        protected virtual void OnSetDirty()
        {
            if (Utils.isPlaying) return;
            
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        protected class LayoutItem
        {
            public Action OnChanged;
            public Action OnChangedInPlaymode;
            public Action<LayoutItem> OnChildChanged;
            public Func<bool> OnValidateDraw;
            public Func<bool> OnValidateDrawChildren;

            public string id;
            public SerializedProperty property;
            public GUIContent content;
            public Action action;
            public bool? drawGroupBorder;
            public Group drawGroup = Group.none;
            public bool disabledInPlaymode = false;

            public List<LayoutItem> children;

            private float _priority;
            private int nextIntPriority;

            public float priority
            {
                get { return _priority; }
                set
                {
                    _priority = value;
                    if ((int)value >= nextIntPriority) nextIntPriority = (int)value + 1;
                }
            }

            public LayoutItem this[string childID]
            {
                get
                {
                    if (children == null) return null;

                    string cid = childID;
                    int slashIndex = cid.IndexOf('\\');
                    if (slashIndex != -1)
                    {
                        cid = childID.Substring(0, slashIndex);
                        childID = childID.Substring(slashIndex + 1);
                    }

                    foreach (LayoutItem child in children)
                    {
                        if (child.id == cid)
                        {
                            if (slashIndex != -1) return child[childID];
                            return child;
                        }
                    }
                    return null;
                }
            }

            public LayoutItem(string id)
            {
                this.id = id;
                _priority = nextIntPriority;
                nextIntPriority++;
            }

            public LayoutItem(SerializedProperty property) : this(property.name)
            {
                this.property = property;
            }

            public LayoutItem Add(LayoutItem item)
            {
                if (children == null) children = new List<LayoutItem>();
                children.Add(item);
                return item;
            }

            public LayoutItem Create(string id)
            {
                return Add(new LayoutItem(id));
            }

            public LayoutItem Create(string id, Action action)
            {
                LayoutItem item = Add(new LayoutItem(id));
                item.action = action;
                return item;
            }

            public LayoutItem Create(SerializedProperty property)
            {
                return Add(new LayoutItem(property));
            }

            public void Dispose()
            {
                property = null;
                content = null;
                OnChanged = null;
                OnChangedInPlaymode = null;
                OnChildChanged = null;
                OnValidateDraw = null;
                OnValidateDrawChildren = null;

                if (children != null)
                {
                    foreach (LayoutItem item in children) item.Dispose();
                    children = null;
                }
            }

            public void Draw()
            {
                if (OnValidateDraw != null && !OnValidateDraw()) return;

                bool hasChildren = children != null && children.Count > 0;
                bool needDrawChildren = hasChildren;
                if (drawGroup == Group.validated) needDrawChildren = hasChildren && (OnValidateDrawChildren == null || OnValidateDrawChildren());
                else if (drawGroup == Group.valueOn) needDrawChildren = hasChildren && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;

                bool needDrawGroup = drawGroup != Group.none && needDrawChildren;
                if (drawGroupBorder.HasValue) needDrawGroup = drawGroupBorder.Value;

                if (needDrawGroup) EditorGUILayout.BeginVertical(GUI.skin.box);
                if (disabledInPlaymode) EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

                if (property != null)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(property, content);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (OnChanged != null) OnChanged();
                        if (EditorApplication.isPlaying && OnChangedInPlaymode != null) OnChangedInPlaymode();
                    }
                }
                if (action != null) action();
                if (needDrawChildren)
                {
                    foreach (LayoutItem item in children)
                    {
                        EditorGUI.BeginChangeCheck();
                        item.Draw();
                        if (EditorGUI.EndChangeCheck() && OnChildChanged != null) OnChildChanged(item);
                    }
                }

                if (disabledInPlaymode) EditorGUI.EndDisabledGroup();
                if (needDrawGroup) EditorGUILayout.EndVertical();
            }

            public LayoutItem Insert(int index, LayoutItem item)
            {
                if (children == null) children = new List<LayoutItem>();
                children.Insert(index, item);
                return item;
            }

            public void Remove(string id)
            {
                if (children == null) return;
                children.RemoveAll(delegate (LayoutItem c)
                {
                    if (c.id == id)
                    {
                        c.Dispose();
                        return true;
                    }
                    return false;
                });
            }

            public void Sort()
            {
                if (children == null) return;

                children = children.OrderBy(c => c.priority).ToList();
                foreach (LayoutItem child in children) child.Sort();
            }

            public enum Group
            {
                none,
                valueOn,
                validated,
                always
            }
        }
    }
}