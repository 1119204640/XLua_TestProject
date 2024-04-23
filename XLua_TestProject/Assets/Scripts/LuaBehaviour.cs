using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using XLua;

// [LuaCallCSharp]
public class LuaBehaviour : MonoBehaviour
{

    [Serializable]
    public class Injection {
        public string name;
        public GameObject value;
    }
    
    public TextAsset luaScript; //Lua脚本，通常用TextAsset形式保存
    //同时，如果把lua脚本放到Resource（或其他目录）中进行本地加载，unity不认lua后缀，所以会额外加.txt后缀
    //但如果不打包到安装包，而是下载形式（例如热更）来读脚本则不存在该问题
    public Injection[] injections;

    internal static LuaEnv luaEnv = new LuaEnv();   //所有lua代码都共用这个lua虚拟机
    internal static float lastGCTime = 0;
    internal const float GCInternal = 1;    //1秒

    private Action luaStart;
    private Action luaUpdate;
    private Action luaOnDestroy;

    private LuaTable scriptScopeTable;

    void Awake() {
        scriptScopeTable = luaEnv.NewTable();   //为每个lua脚本设置一个独立的脚本域，一定程度上防止脚本间全局变量、函数的冲突

        using (LuaTable meta = luaEnv.NewTable()) {
            meta.Set("__index", luaEnv.Global);
            scriptScopeTable.SetMetaTable(meta);
        }

        scriptScopeTable.Set("self", this); //将所需值注入到Lua脚本域中
        foreach (var injection in injections) {
            scriptScopeTable.Set(injection.name, injection.value);
        }

        //如果需要在lua脚本中设置全局变量，也可以直接将全局脚本域注入到当前脚本的脚本域中，例如
        // scriptScopeTable.Set("Global", luaEnv.Global);
        //这样就可以通过Global.XXX来访问全局变量
        
        luaEnv.DoString(luaScript.text, luaScript.name, scriptScopeTable);  //执行脚本
        // luaEnv.DoString("require 'LuaTestScript'");
        
        Action luaAwake = scriptScopeTable.Get<Action>("awake");
        scriptScopeTable.Get("start", out luaStart);
        scriptScopeTable.Get("update", out luaUpdate);
        scriptScopeTable.Get("ondestroy", out luaOnDestroy);

        if (luaAwake != null) {
            luaAwake();
        }
    }
    void Start()
    {
        if (luaStart != null) {
            luaStart();
        }
    }

    void Update()
    {
        if (luaUpdate != null) {
            luaUpdate();
        }
        if (Time.time - lastGCTime > GCInternal) { //大于某时间间隔就gc一次
            luaEnv.Tick();
            lastGCTime = Time.time;
        }
    }

    void OnDestroy() {
        if (luaOnDestroy != null) {
            luaOnDestroy();
        }
        scriptScopeTable.Dispose();
        luaOnDestroy = null;
        luaUpdate = null;
        luaStart = null;
        injections = null;
    }
}
