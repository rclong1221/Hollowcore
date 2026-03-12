using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

namespace DIG.Voxel.Editor
{
    /// <summary>
    /// EPIC 15.10: Gizmo utilities for visualizing destruction shapes in Scene View.
    /// </summary>
    public static class VoxelShapeGizmos
    {
        private static readonly Color ShapeColor = new Color(1f, 0.3f, 0.1f, 0.4f);
        private static readonly Color WireColor = new Color(1f, 0.5f, 0.2f, 0.8f);
        private static readonly Color CenterColor = Color.yellow;
        
        /// <summary>
        /// Draw a destruction shape gizmo based on type and parameters.
        /// </summary>
        public static void DrawShape(
            VoxelDamageShapeType shapeType,
            Vector3 position,
            Quaternion rotation,
            float param1,
            float param2 = 0f,
            float param3 = 0f)
        {
            switch (shapeType)
            {
                case VoxelDamageShapeType.Point:
                    DrawPoint(position);
                    break;
                    
                case VoxelDamageShapeType.Sphere:
                    DrawSphere(position, param1);
                    break;
                    
                case VoxelDamageShapeType.Cylinder:
                    DrawCylinder(position, rotation, param1, param2);
                    break;
                    
                case VoxelDamageShapeType.Cone:
                    DrawCone(position, rotation, param1, param2, param3);
                    break;
                    
                case VoxelDamageShapeType.Capsule:
                    DrawCapsule(position, rotation, param1, param2);
                    break;
                    
                case VoxelDamageShapeType.Box:
                    DrawBox(position, rotation, new Vector3(param1, param2, param3));
                    break;
            }
        }
        
        /// <summary>
        /// Draw point damage indicator.
        /// </summary>
        public static void DrawPoint(Vector3 position)
        {
            Handles.color = CenterColor;
            Handles.SphereHandleCap(0, position, Quaternion.identity, 0.2f, EventType.Repaint);
            
            Handles.color = WireColor;
            Handles.DrawWireDisc(position, Vector3.up, 0.5f);
            Handles.DrawWireDisc(position, Vector3.right, 0.5f);
            Handles.DrawWireDisc(position, Vector3.forward, 0.5f);
        }
        
        /// <summary>
        /// Draw sphere damage area.
        /// </summary>
        public static void DrawSphere(Vector3 center, float radius)
        {
            Handles.color = ShapeColor;
            Handles.SphereHandleCap(0, center, Quaternion.identity, radius * 2, EventType.Repaint);
            
            Handles.color = WireColor;
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            
            Handles.color = CenterColor;
            Handles.SphereHandleCap(0, center, Quaternion.identity, 0.1f, EventType.Repaint);
        }
        
        /// <summary>
        /// Draw cylinder damage area.
        /// </summary>
        public static void DrawCylinder(Vector3 center, Quaternion rotation, float radius, float height)
        {
            Vector3 up = rotation * Vector3.up;
            Vector3 top = center + up * (height * 0.5f);
            Vector3 bottom = center - up * (height * 0.5f);
            
            Handles.color = WireColor;
            Handles.DrawWireDisc(top, up, radius);
            Handles.DrawWireDisc(bottom, up, radius);
            Handles.DrawWireDisc(center, up, radius);
            
            // Draw vertical lines
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            
            Handles.DrawLine(top + right * radius, bottom + right * radius);
            Handles.DrawLine(top - right * radius, bottom - right * radius);
            Handles.DrawLine(top + forward * radius, bottom + forward * radius);
            Handles.DrawLine(top - forward * radius, bottom - forward * radius);
            
            // Draw axis
            Handles.color = CenterColor;
            Handles.DrawLine(center, top);
            Handles.DrawLine(center, bottom);
        }
        
        /// <summary>
        /// Draw cone damage area.
        /// </summary>
        public static void DrawCone(Vector3 tip, Quaternion rotation, float angleDegrees, float length, float tipRadius)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            
            Vector3 baseCenter = tip + forward * length;
            float baseRadius = tipRadius + length * Mathf.Tan(angleDegrees * 0.5f * Mathf.Deg2Rad);
            
            Handles.color = WireColor;
            
            // Draw base circle
            Handles.DrawWireDisc(baseCenter, forward, baseRadius);
            
            // Draw tip circle
            if (tipRadius > 0.01f)
            {
                Handles.DrawWireDisc(tip, forward, tipRadius);
            }
            
            // Draw cone lines
            Handles.DrawLine(tip + right * tipRadius, baseCenter + right * baseRadius);
            Handles.DrawLine(tip - right * tipRadius, baseCenter - right * baseRadius);
            Handles.DrawLine(tip + up * tipRadius, baseCenter + up * baseRadius);
            Handles.DrawLine(tip - up * tipRadius, baseCenter - up * baseRadius);
            
            // Draw axis
            Handles.color = CenterColor;
            Handles.DrawLine(tip, baseCenter);
            Handles.SphereHandleCap(0, tip, Quaternion.identity, 0.1f, EventType.Repaint);
        }
        
        /// <summary>
        /// Draw capsule damage area.
        /// </summary>
        public static void DrawCapsule(Vector3 center, Quaternion rotation, float radius, float length)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 start = center - forward * (length * 0.5f);
            Vector3 end = center + forward * (length * 0.5f);
            
            Handles.color = WireColor;
            
            // Draw end spheres
            Handles.DrawWireDisc(start, forward, radius);
            Handles.DrawWireDisc(end, forward, radius);
            
            // Draw perpendicular discs
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            
            Handles.DrawWireDisc(start, right, radius);
            Handles.DrawWireDisc(start, up, radius);
            Handles.DrawWireDisc(end, right, radius);
            Handles.DrawWireDisc(end, up, radius);
            
            // Draw connecting lines
            Handles.DrawLine(start + right * radius, end + right * radius);
            Handles.DrawLine(start - right * radius, end - right * radius);
            Handles.DrawLine(start + up * radius, end + up * radius);
            Handles.DrawLine(start - up * radius, end - up * radius);
            
            // Draw axis
            Handles.color = CenterColor;
            Handles.DrawLine(start, end);
        }
        
        /// <summary>
        /// Draw oriented bounding box damage area.
        /// </summary>
        public static void DrawBox(Vector3 center, Quaternion rotation, Vector3 extents)
        {
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            
            Handles.color = WireColor;
            Handles.DrawWireCube(Vector3.zero, extents * 2);
            
            Handles.color = CenterColor;
            Handles.SphereHandleCap(0, Vector3.zero, Quaternion.identity, 0.1f, EventType.Repaint);
            
            Handles.matrix = oldMatrix;
        }
        
        /// <summary>
        /// Draw a VoxelDamageRequest shape for debugging.
        /// </summary>
        public static void DrawRequest(VoxelDamageRequest request)
        {
            DrawShape(
                request.ShapeType,
                request.TargetPosition,
                request.TargetRotation,
                request.Param1,
                request.Param2,
                request.Param3
            );
            
            // Draw source line
            Handles.color = Color.cyan;
            Handles.DrawDottedLine(request.SourcePosition, request.TargetPosition, 3f);
        }
    }
}
