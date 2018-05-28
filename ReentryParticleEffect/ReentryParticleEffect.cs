using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ReentryParticle
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ReentryParticleEffect : MonoBehaviour
    {
        public Vector3 Velocity;
        public int MaxParticles = 3000;
        public int MaxEmissionRate = 400;
        // Minimum reentry strength that the effects will activate at.
        // 0 = Activate at the first sign of the flame effects.
        // 1 = Never activate, even at the strongest reentry strength.
        public float EffectThreshold = 0.5f;

        private void Start()
        {
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);
        }

        public class ReentryEffect
        {
            public ReentryEffect(GameObject effect)
            {
                ParticleSystem[] particleSystems = effect.GetComponentsInChildren<ParticleSystem>();
                Trail = particleSystems[0];
                Trail.startSize = (float) 3.5;
                Trail.startSpeed = (float) 3.5;
                Sparks = particleSystems[1]; 
                FXPrefab[] prefabs = effect.GetComponentsInChildren<FXPrefab>();
                trailPrefab = prefabs[0];
            }
            public FXPrefab trailPrefab;
            public ParticleSystem Trail;
            public ParticleSystem Sparks;

            public void Die ()
            {
                Destroy (trailPrefab);
                Destroy (Trail);
                Destroy (Sparks);
            }
        }

        public ReentryEffect GetEffect()
        {
            GameObject effect = (GameObject)GameObject.Instantiate(Resources.Load("Effects/fx_reentryTrail"));
            ReentryEffect reentryFx = new ReentryEffect(effect);
            
            reentryFx.Trail.playbackSpeed = 5;
            reentryFx.Sparks.playbackSpeed = 5;
            return reentryFx;
        }

        public Dictionary<Guid, ReentryEffect> VesselDict = new Dictionary<Guid, ReentryEffect>();

        private void FixedUpdate()
        {
            float effectStrength = (AeroFX.FxScalar * AeroFX.state - EffectThreshold) * (1 / EffectThreshold);
            List<Vessel> vessels = FlightGlobals.Vessels;
            for (int i = vessels.Count - 1; i >= 0; --i)
            {
                Vessel vessel = vessels[i];
                ReentryEffect effects  = null;
                if (VesselDict.ContainsKey(vessel.id))
                    effects = VesselDict[vessel.id];
                else
                {
                    if (vessel.loaded)
                    {
                        effects = GetEffect();
                        VesselDict.Add(vessel.id, effects);
                    }
                    else
                        continue;
                }

                if (!vessel.loaded)
                {
                    if (effects != null)
                    {
                        effects.Die ();
                    }
                    effects = null;
                    continue;
                }

                if (effects == null || effects.Trail == null || effects.Sparks == null)
                    continue;

                if (AeroFX != null)
                {
                    // FxScalar: Strength of the effects.
                    // state: 0 = condensation, 1 = reentry.
                    if (effectStrength > 0)
                    {
                        // Ensure the particles don't lag a frame behind.
                        effects.Trail.scalingMode = ParticleSystemScalingMode.Local;
                        effects.Trail.transform.position = vessel.CoM + vessel.rb_velocity * Time.fixedDeltaTime;
                        effects.Trail.transform.localScale = new Vector3((float).15, (float).15, (float).15); 
                        effects.Trail.enableEmission = true;
                        effects.Sparks.transform.position = vessel.CoM + vessel.rb_velocity * Time.fixedDeltaTime;
                        effects.Sparks.enableEmission = true;

                        Velocity = AeroFX.velocity * (float)AeroFX.airSpeed;

                        effects.Trail.startSpeed = Velocity.magnitude;
                        effects.Trail.transform.forward = -Velocity.normalized;
                        effects.Trail.maxParticles = (int)(MaxParticles * effectStrength);
                        effects.Trail.emissionRate = (int)(MaxEmissionRate * effectStrength);

                        // startSpeed controls the emission cone angle. Greater than ~1 is too wide.
                        //reentryTrailSparks.startSpeed = velocity.magnitude;
                        effects.Sparks.transform.forward = -Velocity.normalized;
                        effects.Sparks.maxParticles = (int)(MaxParticles * effectStrength);
                        effects.Sparks.emissionRate = (int)(MaxEmissionRate * effectStrength);
                    }
                    else
                    {
                        effects.Trail.enableEmission = false;
                        effects.Sparks.enableEmission = false;
                    }
                }
                else
                {
                    effects.Trail.enableEmission = false;
                    effects.Sparks.enableEmission = false;
                }
            }
        }

        public void OnVesselDestroy(Vessel vessel)
        {
            if (VesselDict.ContainsKey(vessel.id))
            {
                ReentryEffect effects = VesselDict[vessel.id];
                if (effects != null)
                {
                    effects.Die ();
                }
                VesselDict.Remove(vessel.id);
            }
        }

        private AerodynamicsFX _aeroFX;
        AerodynamicsFX AeroFX
        {
            get
            {
                if (_aeroFX == null)
                {
                    GameObject fxLogicObject = GameObject.Find("FXLogic");
                    if (fxLogicObject != null)
                        _aeroFX = fxLogicObject.GetComponent<AerodynamicsFX>();
                }
                return _aeroFX;
            }
        }
    }
}
