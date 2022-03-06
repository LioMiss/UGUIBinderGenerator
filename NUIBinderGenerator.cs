using ETModel;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class NUIBinderGenerator
{
    // 默认导出组件
    static List<string> componentNames = new List<string>() {
        "NUIListView",
        "NUIListItem",
        "NCommonPanel",
        "RectTransform",
        "Animator",
        "UITweenPosition",
        "UITweenSlider",
        "EventTrigger",
        "InputField",
        "Dropdown",
        "Slider",
        "ToggleGroup",
        "Toggle",
        "Button",
        "Image",
        "RawImage",
        "Text",
    };

    // 通过名称指定导出的组件字典
    static Dictionary<string, string> componentFieldNames = new Dictionary<string, string>() {
        { "ListView","NUIListView"},
        { "NUIListView","NUIListView"},
        { "ListItem","NUIListItem"},
        { "NUIListItem","NUIListItem"},
        { "TweenPosition","UITweenPosition"},
        { "TweenSlider","UITweenSlider"},
        { "InputField", "InputField"},
        { "Dropdown", "Dropdown"},
        { "Slider","Slider" },
        { "ToggleGroup","ToggleGroup" },
        { "Toggle","Toggle" },
        { "Button","Button" },
        { "Btn","Button" },
        { "RawImage", "RawImage" },
        { "Image","Image" },
        { "Text","Text" },
        { "EventTrigger","EventTrigger" },
        { "Rect","RectTransform" },
        { "RectTransform","RectTransform" },
        { "Transform","Transform" },
        { "GameObject","GameObject" },
        { "Animation", "Animation" },
        { "Animator", "Animator" }
    };

    /// <summary>
    /// 导出目标及其子UI的绑定代码
    /// </summary>
    [MenuItem("Assets/Generator/UIBinder")]
    public static void GeneratorUIBinder()
    {
        Generate(Selection.activeGameObject);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// 不导出子UI
    /// </summary>
    [MenuItem("Assets/Generator/UIBinder(ExcludeChildUI)")]
    public static void GeneratorUIBinder2()
    {
        Generate(Selection.activeGameObject, false);
        AssetDatabase.Refresh();
    }


    [MenuItem("Assets/Generator/UIBinder", true)]
    static bool ValidateGeneratorUIBinder()
    {
        return null != Selection.activeGameObject;
    }


    private static void Generate(GameObject obj, bool exportChildUI = true)
    {
        var className = obj.name.StartsWith("UI") ? "N" + obj.name : "NUI" + obj.name;
        var fileName = className + "Binder.cs";
        List<GameObjectInfo> all = new List<GameObjectInfo>();
        GameObjectInfo start = new GameObjectInfo("", "", obj);
        Dictionary<string, int> UIComponentCount = new Dictionary<string, int>();//以UI为开头的同名节点计数
        Dictionary<string, int> UISameNameCount = new Dictionary<string, int>();//以UI为开头的同路径且同名节点计数
        all.Add(start);
        FindGameObject(start, all, exportChildUI);
        List<ComponentInfo> needOutput = new List<ComponentInfo>();
        for (int i = 0; i < all.Count; i++)
        {
            GameObjectInfo info = all[i];
            if (!info.name.StartsWith("m_"))
            {
                if (info.name.StartsWith("UI"))//UI起始的节点导出
                {
                    if (!UISameNameCount.TryGetValue(info.path, out var sameCount))
                    {
                        UISameNameCount.Add(info.path, 0);
                    }
                    else{
                        UISameNameCount[info.path]++;
                    }
                    if (!UIComponentCount.TryGetValue(info.name, out int count))
                    {
                        UIComponentCount.Add(info.name, 0);
                        needOutput.Add(new ComponentInfo(info, "GameObject", info.name));
                    }
                    else
                    {
                        UIComponentCount[info.name]++;
                        needOutput.Add(new ComponentInfo(info, "GameObject", info.name + UIComponentCount[info.name], UISameNameCount[info.path]));
                    }
                }
                continue;
            }
            // 通过节点名指定组件名
            string result = FindComponentName(info.name);
            if (!string.IsNullOrEmpty(result))
            {
                var name = info.name.Replace(result, "").Replace(componentFieldNames[result], "");
                if (name == "m_")
                {
                    name = GetFieldName(info.gameObject.transform.parent.name) + result;
                }
                else
                {
                    name = info.name;
                }
                // 看有没有N开头的组件
                string componentName = "N" + result;
                if (info.gameObject.GetComponent(componentName) == null)
                {
                    componentName = result;
                    if (info.gameObject.GetComponent(componentName) == null)
                    {
                        if (componentName.Contains("Button"))
                        {
                            if (info.gameObject.GetComponent<EmptyButton>() != null)
                            {
                                componentName = "EmptyButton";
                            }
                            else
                            {
                                Debug.LogError($"{info.name} 找不到组件: {componentName}");
                                return;
                            }
                        }
                        else if (componentName != "RectTransform" && componentName != "Transform" && componentName != "GameObject")
                        {
                            Debug.LogError($"{info.name} 找不到组件: {componentName}");
                            return;
                        }
                    }
                }
                needOutput.Add(new ComponentInfo(info, componentName, name));
            }
            else
            {
                bool hasComponent = false;
                // 导出所有需要的组件
                for (int j = 0; j < componentNames.Count; j++)
                {
                    if (info.gameObject.GetComponent($"N{componentNames[j]}") != null)
                    {
                        hasComponent = true;
                        needOutput.Add(new ComponentInfo(info, "N" + componentNames[j], info.name + FindFieldName(componentNames[j])));
                    }
                    else if (info.gameObject.GetComponent(componentNames[j]) != null)
                    {
                        hasComponent = true;
                        needOutput.Add(new ComponentInfo(info, componentNames[j], info.name + FindFieldName(componentNames[j])));
                    }
                }
                // 没有符合条件的组件 则绑定RectTransform
                if (!hasComponent)
                {
                    needOutput.Add(new ComponentInfo(info, "Transform", info.name));
                }
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using ETHotfix;");
        sb.AppendLine("using ETModel;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.UI;");
        sb.AppendLine("using UnityEngine.EventSystems;");
        sb.AppendLine();
        sb.AppendLine("namespace ETHotfix\r\n{");
        sb.AppendLine("    public partial class " + className + " : NPanel\r\n    {");
        for (int i = 0; i < needOutput.Count; i++)
        {
            sb.AppendLine("        private " + needOutput[i].componentName + " " + needOutput[i].fieldName + ";");
        }
        sb.AppendLine();
        sb.AppendLine("        protected override void InitUIBinder()\r\n        {");
        for (int i = 0; i < needOutput.Count; i++)
        {
            if(needOutput[i].componentName == "Transform" || needOutput[i].componentName == "RectTransform")
            {
                if (needOutput[i].objectInfo.gameObject.GetComponent<RectTransform>() != null)
                {
                    sb.AppendLine("            " + needOutput[i].fieldName + " = transform.FindChildComponent<RectTransform>(\"" + needOutput[i].objectInfo.path + "\");");
                }
                else
                {
                    sb.AppendLine("            " + needOutput[i].fieldName + " = transform.Find(\"" + needOutput[i].objectInfo.path + "\");");
                }                
            }
            else if(needOutput[i].componentName == "GameObject")
            {
                if(needOutput[i].index == 0)
                    sb.AppendLine("            " + needOutput[i].fieldName + " = transform.Find(\"" + needOutput[i].objectInfo.path + "\").gameObject;");
                else
                {
                    var index = needOutput[i].index;
                    sb.AppendLine("            " + needOutput[i].fieldName + " = transform.Find(\"" + needOutput[i].objectInfo.path + "\", " + index + ").gameObject;");
                }                    
            }
            else
            {
                sb.AppendLine("            " + needOutput[i].fieldName + " = transform.FindChildComponent<" + needOutput[i].componentName + ">(\"" + needOutput[i].objectInfo.path + "\");");
            }            
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        string path = "Assets/Hotfix/Scripts/UIBinder/" + fileName;
        WriteToFile(path, sb.ToString());
        Debug.Log("UI绑定代码导出完成, Path = " + path);
    }


    private static string FindFieldName(string component)
    {
        foreach (var kvp in componentFieldNames)
        {
            if (kvp.Value == component)
            {
                return kvp.Key;
            }
        }
        return string.Empty;
    }


    /// <summary>
    /// 查找节点名中是否包含组件名
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static string FindComponentName(string name)
    {
        foreach(var kvp in componentFieldNames)
        {
            if (name.EndsWith(kvp.Key))
            {
                return kvp.Value;
            }
        }
        return string.Empty;
    }


    /// <summary>
    /// 将节点名转成字段名
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static string GetFieldName(string name)
    {
        name = name.Replace("m_", "");
        for (int i = 0; i < componentNames.Count; i++)
        {
            if (name.EndsWith(componentNames[i]))
            {
                return "m_" + name.Replace(componentNames[i], "");
            }
        }
        return "m_" + name;
    }


    private static void FindGameObject(GameObjectInfo start, List<GameObjectInfo> all, bool exportChildUI)
    {
        if (start == null || start.gameObject == null || start.gameObject.transform.childCount == 0) return;
        for (int j = 0; j < start.gameObject.transform.childCount; j++)
        {
            Transform child = start.gameObject.transform.GetChild(j);
            if (child.gameObject.name.StartsWith("UI"))
            {
                GameObjectInfo info = new GameObjectInfo(start.path + ((start.path != "") ? "/" : "") + child.name, child.name, child.gameObject);
                all.Add(info);
                if(exportChildUI)
                    Generate(child.gameObject);
            }
            else if(child.gameObject.name.StartsWith("_"))
            {

            }
            else
            {
                GameObjectInfo info = new GameObjectInfo(start.path + ((start.path != "") ? "/" : "") + child.name, child.name, child.gameObject);
                all.Add(info);
                FindGameObject(info, all, exportChildUI);
            }
        }
    }

    private static void WriteToFile(string path, string str)
    {
        if (File.Exists(path))
        {           
            try
            {
                NEditorUtility.P4Operate(NEditorUtility.Config.P4UserName, NEditorUtility.Config.P4Password, NEditorUtility.Config.P4Workspace, NEditorUtility.Config.P4Server, "edit", path.Replace("\\../../client/Assets", ""));
            }
            catch
            {
                if (!UnityEditor.VersionControl.Provider.isActive)
                {
                    EditorUtility.DisplayDialog("导出失败", "检查P4配置是否正确", "确定");
                    return;
                }
                // Unity自带的 异步操作需等待
                UnityEditor.VersionControl.Task task = UnityEditor.VersionControl.Provider.Checkout(path, UnityEditor.VersionControl.CheckoutMode.Asset);
                task.Wait();
            }                   
        }
        //定义写文件流
        FileStream fsw = new FileStream(path, FileMode.Create);
        //字符串转byte[]
        byte[] writeBytes = Encoding.UTF8.GetBytes(str);
        //写入
        fsw.Write(writeBytes, 0, writeBytes.Length);
        //关闭文件流
        fsw.Close();
        AssetDatabase.Refresh();
    }

    class GameObjectInfo
    {
        public string path;
        public string name;
        public GameObject gameObject;

        public GameObjectInfo(string path, string name, GameObject go)
        {
            this.path = path;
            this.gameObject = go;
            this.name = name;
        }
    }

    class ComponentInfo
    {
        public GameObjectInfo objectInfo;
        public string componentName;
        public string fieldName;
        public int index = 0;

        public ComponentInfo(GameObjectInfo objectInfo, string componentName, string fieldName, int index = 0)
        {
            this.objectInfo = objectInfo;
            this.componentName = componentName;
            this.fieldName = fieldName;
            this.index = index;
        }
    }
}