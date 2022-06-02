using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System.Linq;
using System.Text;
using System.Reflection;

/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
#region Utility functions
  /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
  /// <param name="text">String to print.</param>
  private void Print(string text) { /* Implementation hidden. */ }
  /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
  /// <param name="format">String format.</param>
  /// <param name="args">Formatting parameters.</param>
  private void Print(string format, params object[] args) { /* Implementation hidden. */ }
  /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj) { /* Implementation hidden. */ }
  /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }
#endregion

#region Members
  /// <summary>Gets the current Rhino document.</summary>
  private readonly RhinoDoc RhinoDocument;
  /// <summary>Gets the Grasshopper document that owns this script.</summary>
  private readonly GH_Document GrasshopperDocument;
  /// <summary>Gets the Grasshopper script component that owns this script.</summary>
  private readonly IGH_Component Component;
  /// <summary>
  /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
  /// Any subsequent call within the same solution will increment the Iteration count.
  /// </summary>
  private readonly int Iteration;
#endregion

  /// <summary>
  /// This procedure contains the user code. Input parameters are provided as regular arguments,
  /// Output parameters as ref arguments. You don't have to assign output parameters,
  /// they will have a default value.
  /// </summary>
  private void RunScript(List<System.Drawing.Color> Colors, bool Enable, bool Rename, bool RunOnce, bool Debug)
  {
    msg.Clear();


    this.Component.Message = "id: " + id.ToString();

    _debug = Debug; //Set to true to have all events printed in rhino log
    _rename = Rename;

    if (Enable)
    {
      msg.AppendFormat("Set up events on ID {0}\n", id);
      SetupDict(); // Create a dictionary of the input swatches
      SetEventHandlers(); // This would do the job.


    }
    else
    {
      RemoveEventHandlers();
    }
    if (RunOnce)
    {
      foreach(Grasshopper.Kernel.Special.GH_Group grp in Grasshopper.Instances.ActiveCanvas.Document.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>())
      {
        ColorGroup(grp, null); // You decide if it should run once on all existing groups

      }
    }

    Print(msg.ToString());


    // Component setup
    Component.Description = "Set up event handlers to color groups based on prefixes such as 'in_' and 'out_'.\n"
      + "Please insert only swatcheds directly into the Colors input\n\n"
      + "MIT License. Copyright Mathias Sønderskov Schaltz 2022";
    Component.Params.Input[0].Description = "Please insert only swatcheds directly into the Colors input.\nRemember to rename them";
    Component.Params.Input[1].Description = "Toggle to run...";
    Component.Params.Input[2].Description = "Set to true if you want to automatically rename the groups and remove prefix. Default is false.";
    Component.Params.Input[3].Description = "Set to true to run it once and color all existing groups.";
    Component.Params.Input[Component.Params.Input.Count - 1].Description = "Set debug to true to get all the events printed in the rhino log";
  }

  // <Custom additional code> 

  public static Random rnd = new Random();
  public int id = rnd.Next(0, 1000);
  StringBuilder msg = new StringBuilder();
  Dictionary<string, System.Drawing.Color> colorDict = new Dictionary<string, System.Drawing.Color>();

  bool _debug = false;
  bool _rename = false;

  public bool IdExists(int id)
  {
    IList<IGH_DocumentObject> objs = Grasshopper.Instances.ActiveCanvas.Document.Objects;
    return objs
      .OfType<IGH_Component>()
      .Where(ob => ob.NickName == this.Component.NickName)
      .Where(ob => ob.GetType().ToString() == "ScriptComponents.Component_CSNET_Script")
      .Where(ob => ob.Message == "id: " + id.ToString()).Any();

  }


  public void SetupDict()
  {
    colorDict.Clear();
    // Get all inputs to "Colors"
    IList<IGH_Param> colorSources = this.Component.Params.Input[0].Sources;
    if (colorSources.Count == 0)
    {
      throw new Exception("You forgot to setup the colors. Please input swatch components and rename them");
    }
    foreach(IGH_Param source in colorSources)
    {
      //if (source.Type.ToString() == "Grasshopper.Kernel.Types.GH_Colour")
      if (source is Grasshopper.Kernel.Special.GH_ColourSwatch)
      {
        string nickName = source.NickName.Split('_')[0].Trim() + "_";
        if(!colorDict.ContainsKey(nickName))
        {
          Grasshopper.Kernel.Special.GH_ColourSwatch swatch = (Grasshopper.Kernel.Special.GH_ColourSwatch) source;
          colorDict.Add(nickName, swatch.SwatchColour);
        }
        else
        {
          throw new Exception("Multiple swatches using the same name detected. Please rename");
        }
      }
      else
      {
        throw new Exception("Whacko! Why did you input a non-swatch into the Colors? " + source.Type.ToString());
      }

    }

    msg.AppendFormat("Set up the dictionaries with {0} colors\n", colorDict.Count);
    foreach(var pairs in colorDict)
    {
      msg.AppendFormat("{0} -> {1}\n", ("\"" + pairs.Key + "\"").PadRight(5 + colorDict.Keys.Select(k => k.Length).Max()), pairs.Value);
    }
  }

  public void SetEventHandlers()
  {

    // Always remove events first to avoid duplicates
    GrasshopperDocument.ObjectsAdded -= OnObjectsAdded;
    GrasshopperDocument.ObjectsDeleted -= OnObjectsRemoved;
    GrasshopperDocument.ObjectsDeleted -= OnDeleteThisComponent;

    // Attach events
    GrasshopperDocument.ObjectsAdded += OnObjectsAdded;
    GrasshopperDocument.ObjectsDeleted += OnObjectsRemoved;
    GrasshopperDocument.ObjectsDeleted += OnDeleteThisComponent;


    foreach(Grasshopper.Kernel.Special.GH_Group grp in GrasshopperDocument.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>())
    {
      grp.ObjectChanged -= ColorGroup;
      grp.ObjectChanged += ColorGroup;

      //ColorGroup(grp, null); // Make sure to run it once
    }
    msg.AppendFormat("Added the eventhandlers on {0} existing groups and on future created groups\n", GrasshopperDocument.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>().Count());
  }

  public void OnObjectsAdded(object sender, GH_DocObjectEventArgs e)
  {
    bool any = false;
    foreach (Grasshopper.Kernel.Special.GH_Group grp in e.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>())
    {
      grp.ObjectChanged -= ColorGroup;
      grp.ObjectChanged += ColorGroup;
      any = true;
    }

    DebugWrite(any ? "Group Objects Added: Attaching Events to ObjectChanged" : "Objects Added - but no groups");
  }

  //Same but reversed
  public void OnObjectsRemoved(object sender, GH_DocObjectEventArgs e)
  {
    bool any = false;
    foreach (Grasshopper.Kernel.Special.GH_Group grp in e.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>())
    {
      grp.ObjectChanged -= ColorGroup;
      any = true;
    }

    DebugWrite(any ? "Group Objects Removed, removing event" : "Objects Removed - although not a group");

  }



  public void RemoveEventHandlers()
  {
    GrasshopperDocument.ObjectsAdded -= OnObjectsAdded;
    GrasshopperDocument.ObjectsDeleted -= OnObjectsRemoved;


    foreach(Grasshopper.Kernel.Special.GH_Group grp in GrasshopperDocument.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>())
    {
      grp.ObjectChanged -= ColorGroup;

    }
    msg.AppendFormat("Removed event handlers on document and on {0} groups\n", GrasshopperDocument.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>().Count());

  }

  public void ColorGroup(object sender, EventArgs e)
  {
    if (!IdExists(id))
    {
      DebugWrite("Component not relevant. Disabling old id + " + id.ToString());
      RemoveEventHandlers();
      return;
    }

    Grasshopper.Kernel.Special.GH_Group grp = sender as Grasshopper.Kernel.Special.GH_Group;
    if (grp == null) return; //not type GH_Group
    //if (!grp.NickName.EndsWith("_")) return;

    string[] nameParts = grp.NickName.Split('_');
    if (nameParts.Length <= 1)
      return;
    string prefix = grp.NickName.Split('_')[0].Trim() + '_';

    string prefixes = String.Join(", ", colorDict.Keys.Select(k => "\"" + k + "\""));

    msg.Append("prefix " + prefix + "\n");
    if (colorDict.ContainsKey(prefix))
    {
      //if (grp.NickName.EndsWith("_"))
      grp.Colour = colorDict[prefix];
      if (_rename)
      {
        grp.NickName = grp.NickName.Replace(prefix, "");
      }

      DebugWrite("Colored " + grp.NickName);

    }
    else
    {
      DebugWrite("Group changed but not relevant: prefix " + prefix + " is not among " + prefixes);
    }

  }
  public void DebugWrite(string msg)
  {
    if (_debug)
    {
      Rhino.RhinoApp.WriteLine(String.Format("[ColGrps {0}]: {1}", id, msg));
    }
  }

  public void OnDeleteThisComponent(object sender, GH_DocObjectEventArgs e)
  {

    if (e.Objects.OfType<Grasshopper.Kernel.GH_Component>().Where(o => o.NickName == this.Component.NickName).Any())
    {
      DebugWrite("Removed template component. Removing all the eventhandlers");
      RemoveEventHandlers();
    }

  }

  // </Custom additional code> 
}