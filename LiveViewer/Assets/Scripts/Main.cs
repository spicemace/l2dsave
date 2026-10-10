using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using SimpleFileBrowser;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Main : MonoBehaviour
{
    public EditorUI EditorUi;
    public Button[] buttonList = new Button[3];  // Increased to 3: Open Player, Open Model, Select Folder


    private static XmlElement dataNode;
    public static LiveViewerTools liveViewerTools = new LiveViewerTools();
#if UNITY_ANDROID
    private static AndroidJavaObject toolClass;

    protected static AndroidJavaObject ToolClass
    {
        get
        {
            if (toolClass == null)
            {
                toolClass = new AndroidJavaObject("com.zy.tools.Tool", new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity"));
            }
            return toolClass;
        }

        set
        {
            toolClass = value;
        }
    }
#endif
    IEnumerator Start()
    {
        buttonList[0].onClick.AddListener(OnOpenPlayer);
        buttonList[1].onClick.AddListener(OnOpenModel);
        buttonList[2].onClick.AddListener(OnSelectSaveFolder);

        // Show status based on whether a save folder is selected
        if (!string.IsNullOrEmpty(LiveViewerTools.SelectedSaveRoot))
        {
            PopUI.ShowMessage(LogType.Log, "Save folder: " + Path.GetFileName(LiveViewerTools.SelectedSaveRoot));
        }
        else
        {
            PopUI.ShowMessage(LogType.Log, "Click 'Select Folder' to choose your copied save directory");
        }
        yield return new WaitForSeconds(0.1f);
        // Don't automatically copy files - user will select folder manually
    }

    public static void UserSave(string data)
    {
        if (liveViewerTools.SaveDatString(data, LiveViewerTools.PrefDataPath))
        {
            PopUI.ShowMessage(LogType.Log, "Saved successfully / 保存成功");
        }
        else
        {
            PopUI.ShowMessage(LogType.Error, "Save failed, please check permissions / 保存失败，请检查文件夹权限");
        }
    }

    public static string UserLoad()
    {
        try
        {
            if (string.IsNullOrEmpty(LiveViewerTools.SelectedSaveRoot))
            {
                PopUI.ShowMessage(LogType.Error, "Please select a save folder first / 请先选择保存文件夹");
                return null;
            }
            return liveViewerTools.ReadDatString(LiveViewerTools.PrefDataPath);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            PopUI.ShowMessage(LogType.Error, "Failed to read file: " + e.Message);
            return null;
        }
    }

    public static bool UserCheck()
    {
        return RequestRoot();
    }


    public static bool RequestRoot()
    {
        try
        {
            return ToolClass.Call<bool>("upgradeRootPermission");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogException(e);
        }
#if UNITY_EDITOR
        return true;
#endif
        return false;
    }

    /// <summary>
    /// Opens folder picker to select the save directory.
    /// User should have already copied files from Android/data/com.pavostudio.live2dviewerex to a location like Documents/com.pavostudio.live2dviewerex
    /// </summary>
    public void OnSelectSaveFolder()
    {
        if (FileBrowser.CheckPermission() == FileBrowser.Permission.Granted)
        {
            // Start from Documents or app's home
            string startPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            FileBrowser.ShowLoadDialog(OnFolderSelected, () => { }, true, startPath);
        }
        else
        {
            PopUI.ShowMessage(LogType.Error, "File permission not granted / 文件权限未授予");
        }
    }

    private void OnFolderSelected(string path)
    {
        // Validate that the selected folder contains a 'save' subdirectory
        string savePath = Path.Combine(path, "save");
        if (!Directory.Exists(savePath))
        {
            PopUI.ShowMessage(LogType.Error, "Selected folder must contain a 'save' subdirectory / 选中的文件夹必须包含 'save' 子目录");
            return;
        }

        LiveViewerTools.SelectedSaveRoot = path;
        PopUI.ShowMessage(LogType.Log, "Save folder set to: " + Path.GetFileName(path));
    }

    public void OnOpenPlayer()
    {
        string data = UserLoad();
        if (data == null)
        {
            PopUI.ShowMessage(LogType.Error, "Failed to read save / 读取失败");
            return;
        }
        EditorUi.SetText(data);
        EditorUi.OnSave = UserSave;
    }

    public void OnOpenModel()
    {
        if (string.IsNullOrEmpty(LiveViewerTools.SelectedSaveRoot))
        {
            PopUI.ShowMessage(LogType.Error, "Please select a save folder first / 请先选择保存文件夹");
            return;
        }

        if (FileBrowser.CheckPermission() == FileBrowser.Permission.Granted)
        {
            string chaPath = Path.Combine(LiveViewerTools.SelectedSaveRoot, LiveViewerTools.CharaPath);
            if (!Directory.Exists(chaPath))
            {
                PopUI.ShowMessage(LogType.Error, "Character folder not found at: " + chaPath);
                return;
            }
            FileBrowser.ShowLoadDialog(OnFileOpenSuccess, () => { }, false, chaPath);
        }
        else
        {
            PopUI.ShowMessage(LogType.Error, "File permission not granted / 文件权限未授予");
        }
    }

    private void OnFileOpenSuccess(string path)
    {
        try
        {
            string data = liveViewerTools.ReadDatString(path);
            EditorUi.SetText(data);
            EditorUi.OnSave = (text) => CheckSaveModel(text, path);
        }
        catch (Exception e)
        {
            PopUI.ShowMessage(LogType.Error, "Failed to load file: " + e.Message);
        }
    }

    private bool CheckSaveModel(string text, string path)
    {
        try
        {
            var obj = JsonConvert.DeserializeObject(text);
            liveViewerTools.SaveDatString(text, path);
            PopUI.ShowMessage(LogType.Log, "Character saved / 角色已保存");
            return true;
        }
        catch (Exception e)
        {
            PopUI.ShowMessage(LogType.Exception, e.Message);
        }
        return false;
    }
}