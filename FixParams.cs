using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System.Text;
using System.Linq;
using Grasshopper.Kernel.Special;

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
  private void RunScript(bool FixOnce, bool Enable, bool Debug)
  {
    _debug = Debug;
    _enable = Enable;
    msg.Clear();
    this.Component.Message = "id: " + id.ToString();




    if (Enable)
    {
      SetEventHandlers();
    }
    else
    {
      RemoveEventHandlers();
    }


    if (FixOnce)
    {
      FixAll();
    }

    Print(msg.ToString());

    // Component setup
    Component.Description = "Set up event handlers to change params to show nickname and not icon"
      + "\n\n"
      + "MIT License. Copyright Mathias Sønderskov Schaltz 2022";
    Component.Params.Input[0].Description = "Run me once on the document";
    Component.Params.Input[1].Description = "Toggle to enable event listener";
    Component.Params.Input[Component.Params.Input.Count - 1].Description = "Set debug to true to get all the events printed in the rhino log";
  }

  // <Custom additional code> 
  public static Random rnd = new Random();
  public int id = rnd.Next(0, 1000);
  StringBuilder msg = new StringBuilder();
  bool _debug = false;
  bool _enable = false;

  public Dictionary<IGH_ActiveObject, HashSet<GH_Group>> groupsPerObject = new Dictionary<IGH_ActiveObject, HashSet<GH_Group>>();


  public void OnObjectsAdded(object sender, GH_DocObjectEventArgs e)
  {
    if (!IdExists(id))
    {
      DebugWrite("[FixParam] Component not relevant. Disabling old id " + id.ToString());
      RemoveEventHandlers();
      return;
    }


    if(_enable)
    {
      foreach(IGH_Param par in e.Objects.OfType<IGH_Param>())
      {
        FixInputs(par);
      }
    }


  }

  public void FixAll()
  {
    //DebugWrite("Fixing all?");
    foreach(IGH_Param par in GrasshopperDocument.Objects.OfType<IGH_Param>())
    {

      FixInputs(par);
    }
  }




  public void FixInputs(IGH_ActiveObject obj)
  {



    IGH_Param par = obj as IGH_Param;
    if (par != null)
    {

      par.IconDisplayMode = GH_IconDisplayMode.name;


    }

  }


  public void SetEventHandlers()
  {

    GrasshopperDocument.ObjectsAdded -= OnObjectsAdded;
    GrasshopperDocument.ObjectsAdded += OnObjectsAdded;
    GrasshopperDocument.ObjectsDeleted -= OnDeleteThisComponent;
    GrasshopperDocument.ObjectsDeleted += OnDeleteThisComponent;
    msg.AppendFormat("Added the eventhandlers to OnObjectsAdded\n", GrasshopperDocument.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>().Count());
  }

  public void RemoveEventHandlers()
  {
    GrasshopperDocument.ObjectsAdded -= OnObjectsAdded;
    GrasshopperDocument.ObjectsDeleted -= OnDeleteThisComponent;

    msg.AppendFormat("Removed wire event handlers on document\n", GrasshopperDocument.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>().Count());

  }

  public void OnDeleteThisComponent(object sender, GH_DocObjectEventArgs e)
  {

    if (e.Objects.OfType<Grasshopper.Kernel.GH_Component>().Where(o => o.NickName == this.Component.NickName).Any())
    {
      DebugWrite("Removed template component. Removing all the eventhandlers");
      RemoveEventHandlers();
    }

  }

  public bool IdExists(int id)
  {
    IList<IGH_DocumentObject> objs = Grasshopper.Instances.ActiveCanvas.Document.Objects;
    return objs
      .OfType<IGH_Component>()
      .Where(ob => ob.NickName == this.Component.NickName)
      .Where(ob => ob.GetType().ToString() == "ScriptComponents.Component_CSNET_Script")
      .Where(ob => ob.Message == "id: " + id.ToString()).Any();

  }

  public void DebugWrite(string msg)
  {
    if (_debug)
    {
      Rhino.RhinoApp.WriteLine(String.Format("[FixParams {0}]: {1}", id, msg));
    }
  }
  // </Custom additional code> 
}