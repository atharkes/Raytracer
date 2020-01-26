﻿using OpenTK;
using System.Linq;
using System.Collections.Generic;
using WhittedRaytracer.Raytracing.SceneObjects;
using System;

namespace WhittedRaytracer.Raytracing.AccelerationStructure {
    /// <summary> A node of a bounding volume hierarchy tree </summary>
    class BVHNode {
        /// <summary> The AABB of this BVH node </summary>
        public AABB AABB { get; private set; }
        /// <summary> The left child node if it has one </summary>
        public BVHNode Left { get; private set; }
        /// <summary> The right child node if it has one </summary>
        public BVHNode Right { get; private set; }
        /// <summary> Whether this node is a leaf or not </summary>
        public bool Leaf { get; private set; } = true;
        /// <summary> On which axis the node is split </summary>
        public Vector3 SplitDirection { get; private set; }

        /// <summary> Create a bounding volume hierarchy tree, splitting into smaller nodes if needed </summary>
        /// <param name="primitives">The primitives in the tree</param>
        public BVHNode(List<Primitive> primitives) {
            AABB = new AABB(primitives);
            TrySplit();
        }

        /// <summary> Create a bounding volume hierarchy tree, splitting into smaller nodes if needed </summary>
        /// <param name="aabb">The aabb to create the tree from</param>
        public BVHNode(AABB aabb) {
            AABB = aabb;
            TrySplit();
        }

        /// <summary> Try split the BVH Node into 2 smaller Nodes </summary>
        void TrySplit() {
            Split split = ComputeBestSplit();
            if (split is null) return;
            Left = new BVHNode(split.Left);
            Right = new BVHNode(split.Right);
            SplitDirection = split.Direction;
            Leaf = false;
            AABB = new AABB(Left.AABB, Right.AABB);
        }

        /// <summary> Intersect and traverse the BVH with a ray </summary>
        /// <param name="ray">The ray to intersect the BVH with</param>
        /// <returns>The intersection in the BVH</returns>
        public Intersection Intersect(Ray ray) {
            if (ray is CameraRay) (ray as CameraRay).BVHTraversals++;
            if (!AABB.Intersect(ray)) {
                return null;
            } else if (Leaf) {
                return AABB.IntersectPrimitives(ray);
            } else {
                return IntersectChildren(ray); 
            }
        }

        /// <summary> Intersect the children of this BHV node </summary>
        /// <param name="ray">The ray to intersect the children with</param>
        /// <returns>The intersection in the children if there is any</returns>
        Intersection IntersectChildren(Ray ray) {
            Intersection firstIntersection;
            Intersection secondIntersection;
            if (Vector3.Dot(SplitDirection, ray.Direction) < 0) {
                firstIntersection = Left.Intersect(ray);
                secondIntersection = Right.Intersect(ray);
            } else {
                firstIntersection = Right.Intersect(ray);
                secondIntersection = Left.Intersect(ray);
            }
            if (secondIntersection == null) {
                return firstIntersection;
            } else {
                return secondIntersection;
            }
        }

        /// <summary> Intersect and traverse the BVH with a ray </summary>
        /// <param name="ray">The ray to intersect the BVH with</param>
        /// <returns>Whether there is an intersection with the BVH and the ray</returns>
        public bool IntersectBool(Ray ray) {
            if (!AABB.Intersect(ray)) {
                return false;
            } else if (Leaf) {
                foreach (Primitive primitive in AABB.Primitives) {
                    if (primitive.IntersectBool(ray)) return true;
                }
                return false;
            } else {
                return Left.IntersectBool(ray) || Right.IntersectBool(ray);
            }
        }

        /// <summary> Compute the best split for this BVH node </summary>
        /// <returns>Either a tuple with the best split or null if there is no good split</returns>
        Split ComputeBestSplit() { 
            List<Split> splits = BVH.Bin && BVH.BinAmount < AABB.Primitives.Count ? BinSplits() : CheckAllSplits();
            float bestCost = AABB.SurfaceAreaHeuristic;
            Split bestSplit = null;
            foreach (Split split in splits) {
                float splitCost = split.SurfaceAreaHeuristic;
                if (splitCost < bestCost) {
                    bestSplit = split;
                    bestCost = splitCost;
                }
            }
            return bestSplit;
        }

        /// <summary> Check all possible splits </summary>
        /// <returns>The best split for every axis</returns>
        List<Split> CheckAllSplits() {
            return new List<Split> {
                BestLinearSplitAfterSort(Vector3.UnitX, p => p.AABBCenter.X),
                BestLinearSplitAfterSort(Vector3.UnitY, p => p.AABBCenter.Y),
                BestLinearSplitAfterSort(Vector3.UnitZ, p => p.AABBCenter.Z)
            };
        }

        /// <summary> Split linearly over primitives after using a sorting function </summary>
        /// <param name="sortingFunc">The sorting function to sort the primitives with before finding splits</param>
        /// <returns>The best split using the sorting funciton</returns>
        Split BestLinearSplitAfterSort(Vector3 sortDirection, Func<IAABB, float> sortingFunc) {
            List<IAABB> orderedPrimitives = AABB.Primitives.OrderBy(sortingFunc).ToList();
            Split split = new Split(sortDirection, new AABB(), new AABB(orderedPrimitives));
            IAABB bestSplitPrimitive = orderedPrimitives.FirstOrDefault();
            float bestSplitCost = float.MaxValue;
            foreach (Primitive primitive in orderedPrimitives) {
                split.Left.Add(primitive);
                split.Right.Remove(primitive);
                float splitCost = split.SurfaceAreaHeuristic;
                if (splitCost < bestSplitCost) {
                    bestSplitCost = splitCost;
                    bestSplitPrimitive = primitive;
                }
            }
            int bestSplitPrimitiveIndex = orderedPrimitives.IndexOf(bestSplitPrimitive);
            List<IAABB> primitivesLeft = orderedPrimitives.GetRange(0, bestSplitPrimitiveIndex);
            List<IAABB> primitivesRight = orderedPrimitives.GetRange(bestSplitPrimitiveIndex, orderedPrimitives.Count - bestSplitPrimitiveIndex);
            return new Split(sortDirection, primitivesLeft, primitivesRight);
        }

        List<Split> BinSplits() {
            Vector3 size = AABB.Size;
            Vector3 binDirection = size.X > size.Y && size.X > size.Z ? Vector3.UnitX : (size.Y > size.Z ? Vector3.UnitY : Vector3.UnitZ);
            AABB[] bins = size.X > size.Y && size.X > size.Z ? BinX() : (size.Y > size.Z ? BinY() : BinZ());
            List<Split> splits = new List<Split>(bins.Length - 1);
            for (int i = 1; i < bins.Length; i++) {
                AABB left = new AABB();
                for (int bin = 0; bin < i; bin++) left.AddRange(bins[bin].Primitives);
                AABB right = new AABB();
                for (int bin = i; bin < bins.Length; bin++) right.AddRange(bins[bin].Primitives);
                splits.Add(new Split(binDirection, left, right));
            }
            return splits;
        }

        AABB[] BinX() {
            AABB[] bins = new AABB[BVH.BinAmount];
            for (int i = 0; i < BVH.BinAmount; i++) bins[i] = new AABB();
            float k1 = BVH.BinAmount * BVH.BinningEpsilon / (AABB.MaxBound.X - AABB.MinBound.X);
            foreach (Primitive primitive in AABB.Primitives) {
                int binID = (int)(k1 * (primitive.AABBCenter.X - AABB.MinBound.X));
                bins[binID].Add(primitive);
            }
            return bins;
        }

        AABB[] BinY() {
            AABB[] bins = new AABB[BVH.BinAmount];
            for (int i = 0; i < BVH.BinAmount; i++) bins[i] = new AABB();
            float k1 = BVH.BinAmount * BVH.BinningEpsilon / (AABB.MaxBound.Y - AABB.MinBound.Y);
            foreach (Primitive primitive in AABB.Primitives) {
                int binID = (int)(k1 * (primitive.AABBCenter.Y - AABB.MinBound.Y));
                bins[binID].Add(primitive);
            }
            return bins;
        }

        AABB[] BinZ() {
            AABB[] bins = new AABB[BVH.BinAmount];
            for (int i = 0; i < BVH.BinAmount; i++) bins[i] = new AABB();
            float k1 = BVH.BinAmount * BVH.BinningEpsilon / (AABB.MaxBound.Z - AABB.MinBound.Z);
            foreach (Primitive primitive in AABB.Primitives) {
                int binID = (int)(k1 * (primitive.AABBCenter.Z - AABB.MinBound.Z));
                bins[binID].Add(primitive);
            }
            return bins;
        }
    }
}