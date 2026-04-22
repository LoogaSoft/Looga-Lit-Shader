using UnityEngine;

namespace LoogaSoft.Lighting
{
    [ExecuteAlways]
    public class LoogaWindManager : MonoBehaviour
    {
        [Header("Wind Direction & Speed")]
        public Vector3 windDirection = new(1, 0, 0);
        [Range(0, 10)] public float windSpeed = 2.0f;
        
        [Header("Turbulence")]
        [Range(0, 5)] public float swayStrength = 1.0f;
        [Range(0, 20)] public float flutterSpeed = 10.0f;
        [Range(0, 0.5f)] public float flutterStrength = 0.05f;

        private static readonly int WindDirSpeedID = Shader.PropertyToID("_LoogaWindDirectionAndSpeed");
        private static readonly int WindTurbulenceID = Shader.PropertyToID("_LoogaWindTurbulence");

        private void Update()
        {
            Vector4 dirAndSpeed = new Vector4(windDirection.normalized.x, windDirection.normalized.y, windDirection.normalized.z, windSpeed);
            Shader.SetGlobalVector(WindDirSpeedID, dirAndSpeed);
            
            Vector4 turbulence = new Vector4(swayStrength, flutterSpeed, flutterStrength, 0);
            Shader.SetGlobalVector(WindTurbulenceID, turbulence);
        }
    }
}