using System;
using OpenTK.Mathematics;
using semestral;

public class PlayerController
{
    private Camera camera;
    private Light flashlight;
    private World world;

    public float VerticalVelocity { get; private set; }
    public bool IsGrounded { get; private set; }

    private float playerHeightInternal;
    private float playerCollisionRadiusInternal;
    private float gravityInternal;
    private float lightYOffsetInternal;

    /// <summary>
    /// Initializes a new instance of the PlayerController class.
    /// </summary>
    public PlayerController(Camera controlledCamera, Light associatedFlashlight, World gameWorld,
                            float pHeight, float pRadius, float grav, float lightOffsetY, float WorldStoryHeight)
    {
        this.camera = controlledCamera;
        this.flashlight = associatedFlashlight;
        this.world = gameWorld;

        this.playerHeightInternal = pHeight;
        this.playerCollisionRadiusInternal = pRadius;
        this.gravityInternal = grav;
        this.lightYOffsetInternal = lightOffsetY;

        this.VerticalVelocity = 0f;
        this.IsGrounded = true;
    }

    /// <summary>
    /// Updates the player's vertical state, handling gravity, floor/hole detection, and ceiling collisions.
    /// </summary>
    /// <param name="dt">Delta time for the current frame.</param>
    public void UpdateVerticalState(float dt)
    {
        float currentCameraYBeforeUpdate = camera.position.Y; 

        VerticalVelocity += gravityInternal * dt;
        float candidateY = currentCameraYBeforeUpdate + VerticalVelocity * dt;
        bool landedThisFrame = false;

        if (VerticalVelocity <= 0f) 
        {
            float feetY = candidateY - playerHeightInternal;

            float storyHeight = Program.WorldStoryHeight;

            int currentLevelForGroundCheck = Math.Clamp((int)Math.Round(feetY / storyHeight), -(world.levels - 1), 0);

            float groundSurfaceY = world.GetGroundHeight(camera.position.X, camera.position.Z, currentLevelForGroundCheck);
            if (groundSurfaceY == float.MinValue)
            {
                groundSurfaceY = -1000f; 
                                        
            }

            float headYWhenStandingOnThisGround = groundSurfaceY + playerHeightInternal;

            if (candidateY <= headYWhenStandingOnThisGround)
            {
                candidateY = headYWhenStandingOnThisGround;
                VerticalVelocity = 0f;
                IsGrounded = true;
                landedThisFrame = true;
            }
            else
            {
                IsGrounded = false; // Stále ve vzduchu nad určeným povrchem
            }
        }
        else
        {
            IsGrounded = false;
        }

       
        if (!landedThisFrame)
        {
            Vector3 posCheck = new Vector3(camera.position.X, candidateY, camera.position.Z);
            if (CheckCollisionWithObjects(posCheck)) 
            {
                bool wasAscending = VerticalVelocity > 0f; 

                candidateY = currentCameraYBeforeUpdate;
                VerticalVelocity = 0f; 
                if (!wasAscending) 
                {
                    IsGrounded = true;  
                    landedThisFrame = true; 
                }
            }
        }

        Vector3 newCameraPos = camera.position;
        newCameraPos.Y = candidateY;
        camera.position = newCameraPos;

        // Aktualizace pozice baterky (včetně X a Z)
        if (flashlight != null)
        {
            Vector4 newFlashlightPos = flashlight.position; 
            newFlashlightPos.X = camera.position.X;
            newFlashlightPos.Y = camera.position.Y + lightYOffsetInternal;
            newFlashlightPos.Z = camera.position.Z;
            flashlight.position = newFlashlightPos;
        }
    }

    private bool CheckCollisionWithObjects(Vector3 playerHeadPosNextFrame)
        {
            Vector3 playerMin = new Vector3(
                playerHeadPosNextFrame.X - playerCollisionRadiusInternal,
                playerHeadPosNextFrame.Y - playerHeightInternal,
                playerHeadPosNextFrame.Z - playerCollisionRadiusInternal);
            Vector3 playerMax = new Vector3(
                playerHeadPosNextFrame.X + playerCollisionRadiusInternal,
                playerHeadPosNextFrame.Y + 0.2f,
                playerHeadPosNextFrame.Z + playerCollisionRadiusInternal);
            AABB playerBox = new AABB(playerMin, playerMax);

            foreach (var model in world.worldModels)
            {
                if (model.IsCollidable && model.Material.diffuse != Vector3.Zero)
                {
                    if (model.CollisionBox.Intersects(playerBox)) return true;
                }
            }
            return false;
    }

    /// <summary>
    /// Makes the player jump if they are currently grounded.
    /// </summary>
    /// <param name="jumpForce">The initial upward velocity for the jump.</param>
    public void Jump(float jumpForce)
    {
        if (IsGrounded)
        {
            VerticalVelocity = jumpForce;
            IsGrounded = false;
        }
    }

    /// <summary>
    /// Resets the player's physics state after a teleport.
    /// Called by the teleport system once the player's position has been changed.
    /// </summary>
    public void NotifyTeleported()
    {
        this.VerticalVelocity = 0f;
        this.IsGrounded = true;
        Console.WriteLine("PlayerController state reset after teleport.");
    }
}
