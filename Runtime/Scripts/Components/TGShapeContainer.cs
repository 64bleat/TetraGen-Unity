using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TetraGen
{
    public class TGShapeContainer : MonoBehaviour
    {

        public TetraGenShape[] shapes = null;

        private void OnValidate()
        {
            RefreshShapeList();
        }

        private void Awake()
        {
            RefreshShapeList();
        }

        public void RefreshShapeList()
        {
            shapes = transform.GetComponentsInChildren<TetraGenShape>();
        }
    }
}
