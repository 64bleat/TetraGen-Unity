using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TetraGen
{
    public class TGShapeContainer : MonoBehaviour
    {

        public readonly List<TetraGenShape> shapes = new List<TetraGenShape>();

        private void Start()
        {
            shapes.AddRange(transform.GetComponentsInChildren<TetraGenShape>());
        }
    }
}
