﻿using OpenTK.Mathematics;
using System;

namespace PathTracer.Pathtracing.SceneObjects.Primitives {
    /// <summary> A quad primitive </summary>
    public class Rectangle : Primitive {

        public override Vector3[] Bounds => throw new NotImplementedException();

        public override float SurfaceArea => throw new NotImplementedException();

        public override Vector3 GetNormal(Vector3 surfacePoint) {
            throw new NotImplementedException();
        }

        public override float? Intersect(Ray ray) {
            throw new NotImplementedException();
        }

        public override Vector3 PointOnSurface(Random random) {
            throw new NotImplementedException();
        }
    }
}
