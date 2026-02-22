using UnityEngine;

namespace Utilities
{
    public static class Maths
    {
        public static Vector2 GetCameraRelativeXZ(Vector2 rawInput, Transform cameraTransform)
        {
            if (cameraTransform == null)
                return rawInput;

            var forward = cameraTransform.forward;
            var right = cameraTransform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            var worldMove = right * rawInput.x + forward * rawInput.y;
            return new Vector2(worldMove.x, worldMove.z);
        }
    }
}