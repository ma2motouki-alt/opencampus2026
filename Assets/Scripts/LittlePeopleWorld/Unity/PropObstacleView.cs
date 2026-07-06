using LittlePeopleWorld.Domain;
using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    public sealed class PropObstacleView : MonoBehaviour
    {
        SpriteRenderer bodyRenderer;
        SpriteRenderer startCapRenderer;
        SpriteRenderer endCapRenderer;

        public int SourceObstacleId { get; private set; }

        public void Initialize()
        {
            bodyRenderer = CreateRenderer("Body", RuntimeSpriteFactory.Square, 3);
            startCapRenderer = CreateRenderer("Start Cap", RuntimeSpriteFactory.Circle, 3);
            endCapRenderer = CreateRenderer("End Cap", RuntimeSpriteFactory.Circle, 3);
        }

        public void Render(PropObstacle obstacle, NormalizedScreenMapper mapper, bool debugEnabled)
        {
            SourceObstacleId = obstacle.Id;
            bodyRenderer.enabled = debugEnabled;
            startCapRenderer.enabled = false;
            endCapRenderer.enabled = false;

            if (!debugEnabled)
            {
                return;
            }

            var start = mapper.ToWorld(obstacle.Start);
            var end = mapper.ToWorld(obstacle.End);
            var delta = end - start;
            var length = Mathf.Max(0.001f, delta.magnitude);
            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var thickness = Mathf.Max(0.001f, mapper.ToWorldRadius(obstacle.Radius) * 2f);
            var color = new Color(1f, 0.38f, 0.28f, 0.18f);

            transform.position = (start + end) * 0.5f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            bodyRenderer.transform.localPosition = Vector3.zero;
            bodyRenderer.transform.localRotation = Quaternion.identity;
            var squareSize = RuntimeSpriteFactory.Square.bounds.size;
            bodyRenderer.transform.localScale = new Vector3(
                length / Mathf.Max(0.001f, squareSize.x),
                thickness / Mathf.Max(0.001f, squareSize.y),
                1f);
            bodyRenderer.color = color;

            startCapRenderer.color = color;
            endCapRenderer.color = color;
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
