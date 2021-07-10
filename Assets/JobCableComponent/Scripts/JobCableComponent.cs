using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class JobCableComponent : MonoBehaviour
{
    [SerializeField] private Rigidbody _startPoint;

    [SerializeField] private Rigidbody _endPoint;

    [SerializeField] private float _cableLength = 5f;

    [Min(2)] [SerializeField] private int _segments = 5;

    [SerializeField] private int _solverIterations = 2;


    private NativeArray<CableParticle> _cableParticles;

    private int _pointCount;

    public Vector3[] Positions { get; private set; }

    public int Segments => _segments;


    [ContextMenu("Simple Setup CableLength and Segments")]
    private void CalculateCableLength()
    {
        _cableLength = Vector3.Distance(_startPoint.position, _endPoint.position);
        _segments = Mathf.FloorToInt(_cableLength) * 2;
    }

    private void Start()
    {
        _pointCount = Segments + 1;
        Positions = new Vector3[_pointCount];
        _cableParticles = new NativeArray<CableParticle>(_pointCount, Allocator.Persistent);

        var startPosition = _startPoint.position;
        var endPosition = _endPoint.position;

        _cableParticles[0] = new CableParticle
        {
            Bound = true,
            Position = startPosition,
            LastPosition = startPosition,
        };
        _cableParticles[_cableParticles.Length - 1] = new CableParticle
        {
            Bound = true,
            Position = endPosition,
            LastPosition = endPosition,
        };

        for (var index = 1; index < _pointCount; index++)
        {
            var position = Vector3.Lerp(startPosition, endPosition, (float) index / Positions.Length);
            _cableParticles[index] = new CableParticle
            {
                LastPosition = position,
                Position = position,
            };
        }

        for (var index = 0; index < _pointCount; index++)
        {
            Positions[index] = _cableParticles[index].Position;
        }
    }

    private void OnDestroy()
    {
        _cableParticles.Dispose();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        foreach (var position in Positions)
        {
            Gizmos.DrawSphere(position, 0.05f);
        }
    }

    private void FixedUpdate()
    {
        for (var index = 0; index < _pointCount; index++)
        {
            var particle = new CableParticle();

            if (index == 0)
            {
                particle.Bound = true;
                particle.Position = _startPoint.position;
            }
            else if (index == _pointCount - 1)
            {
                particle.Bound = true;
                particle.Position = _endPoint.position;
            }
            else
            {
                particle.LastPosition = Positions[index];
                particle.Position = Positions[index];
            }

            _cableParticles[index] = particle;
        }

        var deltaTime = Time.fixedDeltaTime;
        var gravityDisplacement = deltaTime * deltaTime * Physics.gravity;
        var jobHandle = new JobHandle();
        var verletJob = new VerletJob
        {
            CableParticles = _cableParticles,
            GravityDisplacement = gravityDisplacement,
        };
        var verletJobHandle = verletJob.Schedule(_pointCount, jobHandle);

        var job = new SolveConstraintJob
        {
            CableLength = _cableLength,
            Segments = Segments,
            SolverIterations = _solverIterations,
            CableParticles = _cableParticles
        };

        var schedule = job.Schedule(verletJobHandle);
        schedule.Complete();

        for (var index = 0; index < _pointCount; index++)
        {
            Positions[index] = _cableParticles[index].Position;
        }
    }


    private struct CableParticle
    {
        public bool Bound;
        public float3 Position;
        public float3 LastPosition;
        public float3 Velocity => Position - LastPosition;
    }


    private struct VerletJob : IJobFor
    {
        [ReadOnly] public float3 GravityDisplacement;

        public NativeArray<CableParticle> CableParticles;

        public void Execute(int index)
        {
            var cableParticle = CableParticles[index];

            if (!cableParticle.Bound)
            {
                var position = cableParticle.Position + cableParticle.Velocity + GravityDisplacement;
                cableParticle.LastPosition = cableParticle.Position;
                cableParticle.Position = position;
                CableParticles[index] = cableParticle;
            }
        }
    }


    private struct SolveConstraintJob : IJob
    {
        [ReadOnly] public float CableLength;
        [ReadOnly] public int Segments;
        [ReadOnly] public int SolverIterations;

        public NativeArray<CableParticle> CableParticles;

        public void Execute()
        {
            var segmentLength = CableLength / Segments;
            for (var index = 0; index < SolverIterations; index++)
            {
                SolveConstraints(segmentLength);
            }
        }

        private void SolveConstraints(float segmentLength)
        {
            var length = CableParticles.Length;
            for (var index = 0; index < length - 1; index++)
            {
                var particleA = CableParticles[index];
                var particleB = CableParticles[index + 1];

                var delta = particleB.Position - particleA.Position;
                var lengthAB = math.sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
                var errorFactor = (lengthAB - segmentLength) / lengthAB;

                if (!particleA.Bound && !particleB.Bound)
                {
                    particleA.Position += errorFactor * 0.5f * delta;
                    particleB.Position -= errorFactor * 0.5f * delta;
                }
                else if (!particleA.Bound)
                {
                    particleA.Position += errorFactor * delta;
                }
                else if (!particleB.Bound)
                {
                    particleB.Position -= errorFactor * delta;
                }

                CableParticles[index] = particleA;
                CableParticles[index + 1] = particleB;
            }
        }
    }
}