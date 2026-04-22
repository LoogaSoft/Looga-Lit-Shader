using UnityEngine;

namespace LoogaSoft.Foliage
{
    public class LoogaGrassInteractor : MonoBehaviour
    {
        [Tooltip("How wide of a path this object pushes through the grass.")]
        public float pushRadius = 1.0f;

        private void OnEnable()
        {
            LoogaGrassManager.Register(this);
        }

        private void OnDisable()
        {
            LoogaGrassManager.Deregister(this);
        }
    }
}