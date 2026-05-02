using NSYNK.HyperSlides.Runtime;
using UnityEditor;

namespace NSYNK.HyperSlides.EditorScripts
{
    /// <summary>
    /// This class is used to create a custom editor for the InspectorClassMethods class.
    /// </summary>
    [CustomEditor(typeof(InspectorClassMethods))]
    public class InspectorClassMethods_Inspector : ClassMethodEditor { }
}