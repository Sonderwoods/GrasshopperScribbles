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
  private void RunScript(bool FixOnce, bool Debug, bool Enable)
  {
    _debug = Debug;
    _enable = Enable;
    msg.Clear();
    this.Component.Message = "id: " + id.ToString();


    SetDictionary();

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
  }

  // <Custom additional code> 
  public static Random rnd = new Random();
  public int id = rnd.Next(0, 1000);
  StringBuilder msg = new StringBuilder();
  bool _debug = false;
  bool _enable = false;

  public Dictionary<IGH_ActiveObject, HashSet<GH_Group>> groupsPerObject = new Dictionary<IGH_ActiveObject, HashSet<GH_Group>>();


  public void OnSolutionExpired(object sender, GH_DocUndoEventArgs e)
  {
    if(_enable)
      FixAll();
  }

  public void FixAll()
  {
    DebugWrite("Fixing");
    foreach(IGH_ActiveObject key in groupsPerObject.Keys.Where(k => groupsPerObject[k].Count() > 0))
    {
      FixInputs(key);
    }
  }



  public bool HasGroup(IGH_ActiveObject obj)
  {
    return groupsPerObject[obj].Count > 0;
  }


  public void FixInputs(IGH_ActiveObject obj)
  {

    //List<List<IGH_ActiveObject>> parents = new List<List<IGH_ActiveObject>>();
    IGH_Param par = obj as IGH_Param;
    if (par != null && par.Sources != null)
    {


      IEnumerable<IGH_ActiveObject> p = par.Sources.Select(s => s.Attributes.GetTopLevel.DocObject as IGH_ActiveObject);
      //parents.Add(p);
      if(HasGroup(obj) && p.All(o => !HasGroupsInCommon(o, obj)))
      {
        par.WireDisplay = GH_ParamWireDisplay.faint;
      }

    }

    IGH_Component comp = obj as IGH_Component;
    if (comp != null)
    {
      foreach(IGH_Param inp in comp.Params.Input)
      {

        IEnumerable<IGH_ActiveObject> p = inp.Sources.Select(i => i.Attributes.GetTopLevel.DocObject as IGH_ActiveObject);

        if(HasGroup(obj) && p.All(o => !HasGroupsInCommon(o, obj)))
        {
          inp.WireDisplay = GH_ParamWireDisplay.faint;
        }
      }

    }



  }





  public void SetDictionary()
  {
    IList<IGH_DocumentObject> objects = Grasshopper.Instances.ActiveCanvas.Document.Objects.ToList();
    //Print(String.Join(", ", objects.Select(o => o.NickName)));

    groupsPerObject = objects
      .Where(o => o.GetType() != typeof(GH_Group))
      .OfType<IGH_ActiveObject>()
      .ToDictionary(o => o, o => new HashSet<GH_Group>());

    foreach(GH_Group grp in objects.OfType<GH_Group>())
    {

      foreach(IGH_ActiveObject obj in grp.ObjectsRecursive().OfType<IGH_ActiveObject>())
      {
        groupsPerObject[obj].Add(grp);

      }
    }



  }

  public bool HasGroupsInCommon(IGH_ActiveObject obj1, IGH_ActiveObject obj2)
  {
    HashSet<GH_Group> set1 = groupsPerObject[obj1];
    HashSet<GH_Group> set2 = groupsPerObject[obj2];

    set1.IntersectWith(set2);

    return set1.Any();
  }

  public void SetEventHandlers()
  {

    GrasshopperDocument.UndoStateChanged -= OnSolutionExpired;
    GrasshopperDocument.UndoStateChanged += OnSolutionExpired;
    GrasshopperDocument.ObjectsDeleted -= OnDeleteThisComponent;
    GrasshopperDocument.ObjectsDeleted += OnDeleteThisComponent;
    msg.AppendFormat("Added the eventhandlers to UndoStateChanged\n", GrasshopperDocument.Objects.OfType<Grasshopper.Kernel.Special.GH_Group>().Count());
  }

  public void RemoveEventHandlers()
  {
    GrasshopperDocument.UndoStateChanged -= OnSolutionExpired;
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

  public void DebugWrite(string msg)
  {
    if (_debug)
    {
      Rhino.RhinoApp.WriteLine(String.Format("[ColGrps {0}]: {1}", id, msg));
    }
  }
  // </Custom additional code> 
}