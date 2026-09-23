using System;
using System.IO;
using System.Linq;
namespace StandalonePicturator.Classes;

public static class PicturatorStorage
{
    private static readonly Lazy<string> root = new(Initialize);
    public static string FilePath(string name) => Path.Combine(root.Value,name);
    // Choose one data folder for every installed version; migrate the newest old library only once.
    private static string Initialize()
    {
        string overridePath=Environment.GetEnvironmentVariable("STYX_PICTURATOR_DATA");
        string target=overridePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StyxHavenVN","SliderPicturator");
        Directory.CreateDirectory(target);
        if(overridePath!=null) return target;
        if(File.Exists(Path.Combine(target,"migration.complete"))) return target;
        var candidates=new System.Collections.Generic.List<string>{AppContext.BaseDirectory};
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory!=null && directory.Name!="bin") directory=directory.Parent;
        if(directory!=null)
            candidates.AddRange(Directory.EnumerateFiles(directory.FullName,"picturator_library.json",SearchOption.AllDirectories).Select(Path.GetDirectoryName));
        string source=candidates.Distinct().Where(p=>File.Exists(Path.Combine(p,"picturator_library.json")))
            .OrderByDescending(p=>File.GetLastWriteTimeUtc(Path.Combine(p,"picturator_library.json"))).FirstOrDefault() ?? AppContext.BaseDirectory;
        foreach(string name in new[]{"picturator_session.json","picturator_library.json"}) {
            string old=Path.Combine(source,name), current=Path.Combine(target,name);
            if(File.Exists(old) && !File.Exists(current)) File.Copy(old,current);
        }
        File.WriteAllText(Path.Combine(target,"migration.complete"),source);
        return target;
    }
    // Write a temporary file first, preserve the previous save as .bak, then replace the destination.
    public static void Write(string path,string text)
    {
        string temp=path+".tmp";
        File.WriteAllText(temp,text);
        if(File.Exists(path)) File.Copy(path,path+".bak",true);
        File.Move(temp,path,true);
    }
}

