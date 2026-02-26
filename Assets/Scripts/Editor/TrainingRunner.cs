#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;

/// <summary>
/// Helper to run the training scripts from the Unity editor.
/// </summary>
public static class TrainingRunner
{
    /// <summary>
    /// Run imitation learning.
    /// </summary>
    [MenuItem("ML-Asteroids/Imitation", false, 0)]
    public static void Imitation()
    {
        RunScript("Imitation.bat");
    }
    
    /// <summary>
    /// Run standard training.
    /// </summary>
    [MenuItem("ML-Asteroids/Standard", false, 1)]
    public static void FineTuned()
    {
        RunScript("Standard.bat");
    }
    
    /// <summary>
    /// Monitor the learning in TensorBoard.
    /// </summary>
    [MenuItem("ML-Asteroids/TensorBoard", false, 12)]
    public static void TensorBoard()
    {
        RunScript("Monitor.bat");
        Application.OpenURL("http://localhost:6006");
    }
    
    /// <summary>
    /// Install a Python environment.
    /// </summary>
    [MenuItem("ML-Asteroids/Install", false, 23)]
    public static void Install()
    {
        RunScript("Install.bat");
    }
    
    /// <summary>
    /// Activate the Python environment.
    /// </summary>
    [MenuItem("ML-Asteroids/Activate", false, 24)]
    public static void Activate()
    {
        RunScript("Activate.bat");
    }
    
    /// <summary>
    /// Run a script.
    /// </summary>
    /// <param name="name">The name of the script.</param>
    private static void RunScript([NotNull] string name)
    {
        // Get the directory.
        string directory = Path.GetDirectoryName(Application.dataPath);
        if (directory == null)
        {
            Debug.LogError($"Parent of \"{Application.dataPath}\" does not exist.");
            return;
        }
        
        if (!name.EndsWith(".bat"))
        {
            name = $"{name}.bat";
        }
        
        // Get the file.
        string file = Path.Combine(directory, name);
        if (!File.Exists(file))
        {
            Debug.LogError($"\"{file}\" does not exist.");
            return;
        }
        
        // Start the file in its own process in the correct working directory.
        ProcessStartInfo processInfo = new()
        {
            FileName = file,
            WorkingDirectory = directory,
            UseShellExecute = true
        };
        
        // Try to run it.
        try
        {
            Process.Start(processInfo);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to execute \"{file}\": {e.Message}");
        }
    }
}
#endif