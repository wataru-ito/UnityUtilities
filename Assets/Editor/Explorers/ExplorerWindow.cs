﻿﻿using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


/// <summary>
/// Componentの一覧を表示するエディタの基底クラス
/// </summary>
public abstract class ExplorerWindow<T> : EditorWindow
	where T : UnityEngine.Component
{
	protected class Column
	{
		public readonly string name;
		public readonly GUIContent sepalatorContent;
		public float width;
		public Action<Rect,T> DrawField;

		public Column(string name, float width, Action<Rect,T> DrawField)
		{
			this.name = name;
			this.sepalatorContent = new GUIContent();
			this.width = width;
			this.DrawField = DrawField;
		}
	}

	const float kHeaderHeight = 28f;
	const float kItemHeight = 16f;
	const float kSepalatorWidth = 4;
	const float kItemPaddingX = 4;

	protected List<T> m_itemList = new List<T>();

	GUISkin m_skin;
	protected GUIStyle m_labelStyle;
	Vector2 m_scrollPosition;
	Rect m_scrollRect;


	//------------------------------------------------------
	// unity system function
	//------------------------------------------------------

	protected virtual void OnEnable()
	{
		InitGUI();
	}

	protected virtual void OnFocus()
	{
		GUI.FocusControl(string.Empty);
	}

	protected virtual void OnSelectionChange()
	{
		Repaint();
	}

	protected virtual void OnInspectorUpdate()
	{
		// 他のインスペクタで更新された値を更新するために毎フレーム更新する
		// > インスペクタで値が変更された時のコールバックはないか？
		Repaint();
	}

	protected virtual void OnGUI()
	{
		using (new EditorGUILayout.HorizontalScope())
		{
			GUILayout.Space(12);
			using (new EditorGUILayout.VerticalScope())
			{
                GUILayout.Space(8);
				DrawHeader();
				DrawList();
				GUILayout.Space(4);
			}
			GUILayout.Space(12);
		}

		EventProcedure();
	}


	//------------------------------------------------------
	// abstract methods
	//------------------------------------------------------

	protected abstract MonoScript GetScript();
	protected abstract Column[] GetColumns();
	protected abstract List<T> GetItemList();
	protected abstract void DrawHeader();


	//------------------------------------------------------
	// events
	//------------------------------------------------------

	void EventProcedure()
	{
		switch (Event.current.type)
		{
			case EventType.MouseDown:
				if (m_scrollRect.Contains(Event.current.mousePosition))
				{
					OnCanvasSelected(Event.current);
					Repaint();
				}
				break;
		}
	}

	void OnCanvasSelected(Event ev)
	{
		var index = Mathf.FloorToInt((ev.mousePosition.y - m_scrollRect.y + m_scrollPosition.y) / kItemHeight);
		if (index >= m_itemList.Count)
		{
			Selection.activeGameObject = null;
			return;
		}

		if (IsSelectionAdditive(ev))
		{
			var targetGO = m_itemList[index].gameObject;
			var gos = new List<GameObject>(Selection.gameObjects);
			if (gos.Contains(targetGO))
			{
				gos.Remove(targetGO);
				if (Selection.activeGameObject == targetGO)
				{
					Selection.activeGameObject = gos.Count > 0 ? gos[0] : null;
				}
			}
			else
			{
				gos.Add(targetGO);
			}
			Selection.objects = gos.ToArray();
			return;
		}
		else if (ev.shift)
		{
			var firstItem = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<T>() : null;
			var firstIndex = m_itemList.IndexOf(firstItem);
			if (firstIndex >= 0 && index != firstIndex)
			{
				var diff = index-firstIndex;
				var objects = new UnityEngine.Object[Mathf.Abs(diff)+1];
				var step = diff > 0 ? 1 : -1;
				for (int i = 0; i < objects.Length; ++i, firstIndex+=step)
				{
					objects[i] = m_itemList[firstIndex].gameObject;
				}						
				Selection.objects = objects;
				return;
			}
		}
		
		Selection.activeGameObject = m_itemList[index].gameObject;
	}

	bool IsSelectionAdditive(Event ev)
	{
		#if UNITY_EDITOR_OSX
		return ev.command;
		#else
		return ev.control;
		#endif
	}
	

	//------------------------------------------------------
	// gui
	//------------------------------------------------------

	void InitGUI()
	{
		// GUISKINにアクセスしたいだけなのにMonoScriptを取得する関数を実装してもらわないといけないのは辛い
		// それに違うフォルダに作られたらアクセスできないし
		// > やっぱりPostProcessor作って検知するしかなさそう
		var scriptPath = AssetDatabase.GetAssetPath(GetScript());
		m_skin = AssetDatabase.LoadAssetAtPath<GUISkin>(
			string.Format("{0}/ExplorerWindow.guiskin", Path.GetDirectoryName(scriptPath)));

		m_labelStyle = m_skin.FindStyle("Hi Label");
	}

	void DrawList()
	{
		var columns = GetColumns();
		m_itemList = GetItemList();

		GUILayout.Box(GUIContent.none, 
			GUILayout.ExpandWidth(true),
			GUILayout.ExpandHeight(true));

		var r = GUILayoutUtility.GetLastRect();
		r = DrawColumn(r, columns);

		// この後描画されるbackgrounで枠線が消えてしまうので削る
		r.x += 1f;
		r.width -= 2f;
		r.height -= 1f;
		m_scrollRect = r;

		// アイテムが少なくても全域に表示させる必要があるのでアイテム描画と分けている
		// > スクロールしてると背景と情報表示がずれる…
		DrawBackground();

		var viewRect = new Rect(0, 0, 
			GetListWidth(columns), 
			m_itemList.Count * kItemHeight);
		using (var scroll = new GUI.ScrollViewScope(m_scrollRect, m_scrollPosition, viewRect))
		{
            m_scrollPosition = scroll.scrollPosition;

			var itemPosition = new Rect(0, 0, Mathf.Max(viewRect.width, m_scrollRect.width), kItemHeight);
			foreach (var item in m_itemList)
			{
				itemPosition = DrawItem(itemPosition, columns, item);
			}
		}
	}

	Rect DrawColumn(Rect area, Column[] columns)
	{
		var position = new Rect(area.x, area.y, area.width, kHeaderHeight);
		var viewRect = new Rect(0, 0, area.width, kHeaderHeight);

		GUI.Box(position, GUIContent.none);
		GUI.BeginScrollView(position, m_scrollPosition, viewRect);
		{
			var r = new Rect(
				kItemPaddingX - m_scrollPosition.x, 
				kHeaderHeight - kItemHeight - 2,
				0,
				kItemHeight);

			foreach (var column in columns)
			{
				r.width = column.width;
				EditorGUI.LabelField(r, column.name);
				r.x += r.width;

				r = DrawColumSeparator(r, column);
			}
		}
		GUI.EndScrollView();

		area.y += kHeaderHeight;
		area.height -= kHeaderHeight;
		return area;
	}

	static Rect DrawColumSeparator(Rect r, Column column)
	{
		EditorGUI.LabelField(
			new Rect(
				r.x,
				r.y - 6,
				kSepalatorWidth,
				r.height + 4), 
			column.sepalatorContent, 
			"DopesheetBackground");

		r.x += kSepalatorWidth;
		return r;
	}

	void DrawBackground()
	{
		var prev = GUI.color;
		var gray = new Color(0.95f, 0.95f, 0.95f);
		float y = m_scrollRect.yMin - m_scrollPosition.y;
		for (int i = 0; y < m_scrollRect.yMax; ++i, y += kItemHeight)
		{
			if (y + kItemHeight <= m_scrollRect.yMin) continue;
			if (y >= m_scrollRect.yMax) continue;

			var itemPisition = new Rect(m_scrollRect.x,
				Mathf.Max(y, m_scrollRect.y),
				m_scrollRect.width,
				Mathf.Min(kItemHeight, m_scrollRect.yMax - y));

			GUI.color = i % 2 == 1 ? prev : gray;
			GUI.Box(itemPisition, GUIContent.none, "CN EntryBackOdd");
		}
		GUI.color = prev;
	}

	static float GetListWidth(Column[] columns)
	{
		float width = kItemPaddingX * 2f;
		foreach (var column in columns)
		{
			width += column.width + kSepalatorWidth;
		}
		return width;
	}

	Rect DrawItem(Rect itemPosition, Column[] columns, T item)
	{		
		var styleState = GetStyleState(Selection.gameObjects.Contains(item.gameObject));
		if (styleState.background)
			GUI.DrawTexture(itemPosition, styleState.background);

		var r = itemPosition;
		r.x += kItemPaddingX;
		foreach (var column in columns)
		{
			r.width = column.width;
			column.DrawField(r, item);
			r.x += (r.width + kSepalatorWidth);
		}

		itemPosition.y += r.height;
		return itemPosition;
	}

	GUIStyleState GetStyleState(bool selected)
	{
		if (selected)
			return EditorWindow.focusedWindow == this ? m_labelStyle.onActive : m_labelStyle.onNormal;
		return m_labelStyle.normal;
	}
}
