namespace BossMod.CombatAssistance.Camera;

internal readonly record struct CameraRay(Vector3 Origin, Vector3 Direction, float Distance);
internal readonly record struct CameraRayHit(bool Hit, Vector3 Point);

// Native collision is stable during one synchronous view evaluation. Share only
// exactly identical rays there; execution and the next evaluation read afresh.
internal sealed class CameraRaycast(Func<CameraRay, CameraRayHit> read)
{
    private readonly Dictionary<CameraRay, CameraRayHit> samples = [];
    private bool active;
    internal int Queries { get; private set; }
    internal int Reused { get; private set; }

    internal Scope Begin()
    {
        if (active)
            throw new InvalidOperationException("Camera collision evaluation cannot nest.");
        active = true;
        Queries = Reused = 0;
        return new(this);
    }

    internal CameraRayHit Read(CameraRay ray)
    {
        if (!active)
            return read(ray);
        if (samples.TryGetValue(ray, out var sample))
        {
            Reused++;
            return sample;
        }
        sample = read(ray);
        Queries++;
        samples.Add(ray, sample);
        return sample;
    }

    internal readonly struct Scope(CameraRaycast owner) : IDisposable
    {
        public void Dispose()
        {
            owner.active = false;
            owner.samples.Clear();
        }
    }
}
