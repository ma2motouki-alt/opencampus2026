using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class WalkableSurfaceView : MonoBehaviour
    {
        SpriteRenderer lineRenderer;
        SpriteRenderer startRenderer;
        SpriteRenderer endRenderer;

        public int SourceSurfaceId { get; private set; }

        public void Initialize()
        {
            lineRenderer = CreateRenderer("Path", RuntimeSpriteFactory.Square, 8);
            startRenderer = CreateRenderer("Start", RuntimeSpriteFactory.Circle, 9);
            endRenderer = CreateRenderer("Path End", RuntimeSpriteFactory.Circle, 9);
        }

        public void Render(WalkableSurface surface, NormalizedScreenMapper mapper, bool debugEnabled)
        {
            SourceSurfaceId = surface.Id;
            lineRenderer.enabled = debugEnabled;
            startRenderer.enabled = debugEnabled;
            endRenderer.enabled = debugEnabled;

            if (!debugEnabled)
            {
                return;
            }

            var start = mapper.ToWorld(surface.Start);
            var end = mapper.ToWorld(surface.End);
            var delta = end - start;
            var length = Mathf.Max(0.001f, delta.magnitude);
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var thickness = Mathf.Max(0.006f, mapper.ToWorldRadius(surface.Width) * 0.16f);

            transform.position = (start + end) * 0.5f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            lineRenderer.transform.localPosition = Vector3.zero;
            lineRenderer.transform.localRotation = Quaternion.identity;
            var squareSize = RuntimeSpriteFactory.Square.bounds.size;
            lineRenderer.transform.localScale = new Vector3(
                length / Mathf.Max(0.001f, squareSize.x),
                thickness / Mathf.Max(0.001f, squareSize.y),
                1f);
            lineRenderer.color = new Color(0.1f, 1f, 0.65f, 0.78f);

            var attachLocalX = -length * 0.5f + length * surface.AttachProgress;
            var exitLocalX = -length * 0.5f + length * surface.ExitProgress;
            var endpointScale = Vector3.one * thickness * 2.2f;
            startRenderer.transform.localPosition = new Vector3(attachLocalX, 0f, 0f);
            startRenderer.transform.localScale = endpointScale;
            startRenderer.color = new Color(0.4f, 1f, 0.45f, 0.86f);

            endRenderer.transform.localPosition = new Vector3(exitLocalX, 0f, 0f);
            endRenderer.transform.localScale = endpointScale;
            endRenderer.color = new Color(1f, 0.82f, 0.18f, 0.92f);
        }

        SpriteRenderer CreateRenderer(string name, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
