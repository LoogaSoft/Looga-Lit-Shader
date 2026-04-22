using System.Collections.Generic;
using UnityEngine;

namespace LoogaSoft.Foliage
{
    [ExecuteAlways]
    public class LoogaGrassManager : MonoBehaviour
    {
        private static readonly List<LoogaGrassInteractor> _interactors = new List<LoogaGrassInteractor>();
        
        // We cap it at 64 to keep the GPU constant buffer highly performant. 
        // This is usually plenty for the active camera view in an extraction map.
        private const int MAX_INTERACTORS = 64; 
        private static Vector4[] _interactorData = new Vector4[MAX_INTERACTORS];
        
        private static readonly int InteractorsID = Shader.PropertyToID("_GrassInteractors");
        private static readonly int InteractorCountID = Shader.PropertyToID("_GrassInteractorCount");

        public static void Register(LoogaGrassInteractor interactor)
        {
            if (!_interactors.Contains(interactor)) _interactors.Add(interactor);
        }

        public static void Deregister(LoogaGrassInteractor interactor)
        {
            _interactors.Remove(interactor);
        }

        private void Update()
        {
            int count = Mathf.Min(_interactors.Count, MAX_INTERACTORS);
            
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = _interactors[i].transform.position;
                // Pack XYZ position and W radius into a single Vector4 for the shader
                _interactorData[i] = new Vector4(pos.x, pos.y, pos.z, _interactors[i].pushRadius);
            }

            Shader.SetGlobalVectorArray(InteractorsID, _interactorData);
            Shader.SetGlobalInt(InteractorCountID, count);
        }
    }
}