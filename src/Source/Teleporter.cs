using OpenTK.Mathematics;
using System;

namespace semestral
{
    public class Teleporter
    {
        public Vector3i MapCoordinates { get; private set; }
        public Vector3 WorldPadPosition { get; private set; }
        public Vector3 TargetFeetPosition { get; private set; }
        public Teleporter Partner { get; set; }
        public bool IsActive { get; set; } = true;

        private float activationRadiusSq;
        private float cooldownTimer = 0f;
        private const float COOLDOWN_DURATION = 5.0f;

        private bool playerMustExitZone = false;

        public Teleporter(Vector3i mapCoords, Vector3 worldPadCenterPosition, Vector3 targetFeetWorldPosition, float activationRadius = 1.0f)
        {
            this.MapCoordinates = mapCoords;
            this.WorldPadPosition = worldPadCenterPosition;
            this.TargetFeetPosition = targetFeetWorldPosition;
            this.activationRadiusSq = activationRadius * activationRadius;
        }

        public void UpdateCooldownAndExitState(float dt, Vector3 playerFeetPosition)
        {
            if (cooldownTimer > 0)
            {
                cooldownTimer -= dt;
                if (cooldownTimer < 0) cooldownTimer = 0f;
            }

            if (playerMustExitZone)
            {
                float distanceSq = (new Vector2(WorldPadPosition.X, WorldPadPosition.Z) -
                                    new Vector2(playerFeetPosition.X, playerFeetPosition.Z)).LengthSquared;
                if (distanceSq >= activationRadiusSq)
                {
                    playerMustExitZone = false;
                    Console.WriteLine($"Player exited activation zone of teleporter at Map {MapCoordinates}. Ready for re-activation (after cooldown).");
                }
            }
        }

        public bool CanActivate()
        {
            if (playerMustExitZone) return false;
            return IsActive && cooldownTimer <= 0f;
        }

        public bool TryActivate(Vector3 playerFeetPosition)
        {
            if (!CanActivate()) return false;

            float distanceSq = (new Vector2(WorldPadPosition.X, WorldPadPosition.Z) -
                                new Vector2(playerFeetPosition.X, playerFeetPosition.Z)).LengthSquared;

            return distanceSq < activationRadiusSq;
        }

        public void MarkAsUsedAndStartCooldown()
        {
            cooldownTimer = COOLDOWN_DURATION;
            playerMustExitZone = true;
            Console.WriteLine($"Teleporter at Map {MapCoordinates} (World {WorldPadPosition}) used. Cooldown ({COOLDOWN_DURATION}s) activated and player must exit zone.");

            Partner?.ForcePartnerCooldown();
        }

        public void ForcePartnerCooldown()
        {
            cooldownTimer = COOLDOWN_DURATION;
            playerMustExitZone = false;
            Console.WriteLine($"Teleporter at Map {MapCoordinates} (World {WorldPadPosition}) partner cooldown activated for {COOLDOWN_DURATION}s.");
        }
    }
}