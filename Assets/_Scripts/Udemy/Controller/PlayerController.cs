using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;
using UnityStandardAssets.CrossPlatformInput;
using UnityStandardAssets.Utility;

public class PlayerController : MonoBehaviour
{
    // m_IsWalking
    [SerializeField] private bool is_walking;

    // m_WalkSpeed
    [SerializeField] private float walk_speed;

    // m_RunSpeed
    [SerializeField] private float run_speed;

    // m_UseFovKick
    [SerializeField] private bool use_fov;

    // m_StickToGroundForce
    [SerializeField] private float stick_to_ground;

    // m_GravityMultiplier
    [SerializeField] private float gravity_multiplier;

    // m_MouseLook
    [SerializeField] private MouseLook mouse_look;

    // m_FovKick
    [SerializeField] private FOVKick fov = new FOVKick();

    // m_Camera
    private Camera eye;

    // m_Input
    private Vector2 wasd;

    // m_MoveDir
    private Vector3 move_direction = Vector3.zero;

    // m_CharacterController
    private CharacterController charater_controller;

    // m_CollisionFlags
    private CollisionFlags collision_flags;

    // ==========


    private void Start()
    {
        charater_controller = GetComponent<CharacterController>();
        eye = Camera.main;
        mouse_look.Init(transform, eye.transform);
    }

    // Update is called once per frame
    void Update()
    {
        mouse_look.LookRotation(transform, eye.transform);
    }

    private void FixedUpdate()
    {
        float speed = getInput();

        // desiredMove always move along the camera forward as it is the direction that it being aimed at
        Vector3 direction = transform.forward * wasd.y + transform.right * wasd.x;

        // get a normal for the surface that is being touched to move along it
        RaycastHit hitInfo;
        Physics.SphereCast(transform.position, charater_controller.radius, Vector3.down, out hitInfo,
                           charater_controller.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        direction = Vector3.ProjectOnPlane(direction, hitInfo.normal).normalized;

        move_direction.x = direction.x * speed;
        move_direction.z = direction.z * speed;


        if (charater_controller.isGrounded)
        {
            move_direction.y = -stick_to_ground;
        }
        else
        {
            move_direction += Physics.gravity * gravity_multiplier * Time.fixedDeltaTime;
        }

        collision_flags = charater_controller.Move(move_direction * Time.fixedDeltaTime);

        mouse_look.UpdateCursorLock();
    }

    /// <summary>
    /// 應該是會將 Player 撞到的 rigidbody，沿著速度方向，施加一個 ForceMode.Impulse 類型的力
    /// </summary>
    /// <param name="hit"></param>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // dont move the rigidbody if the character is on top of it
        if (collision_flags == CollisionFlags.Below)
        {
            return;
        }

        Rigidbody body = hit.collider.attachedRigidbody;

        if (body == null || body.isKinematic)
        {
            return;
        }

        body.AddForceAtPosition(charater_controller.velocity * 0.1f, hit.point, ForceMode.Impulse);
    }

    /// <summary>
    /// GetInput
    /// </summary>
    /// <param name="speed"></param>
    private float getInput()
    {
        // Read input
        float horizontal = CrossPlatformInputManager.GetAxis("Horizontal");
        float vertical = CrossPlatformInputManager.GetAxis("Vertical");
        bool was_walking = is_walking;

#if !MOBILE_INPUT
        // On standalone builds, walk/run speed is modified by a key press.
        // keep track of whether or not the character is walking or running
        // 按著 LeftShift 移動，會變成跑步
        is_walking = !Input.GetKey(KeyCode.LeftShift);
#endif

        // set the desired speed to be walking or running
        float speed = is_walking ? walk_speed : run_speed;
        wasd = new Vector2(horizontal, vertical);

        // normalize input if it exceeds 1 in combined length:
        if (wasd.sqrMagnitude > 1)
        {
            wasd.Normalize();
        }

        // handle speed change to give an fov kick
        // only if the player is going to a run, is running and the fovkick is to be used
        // is_walking = False: walking -> running
        // is_walking = True: running -> walking
        if (is_walking != was_walking && use_fov && charater_controller.velocity.sqrMagnitude > 0)
        {
            StopAllCoroutines();

            // TODO: 快速移動時，FOV 應下降，使得視野變窄
            StartCoroutine(is_walking ? fov.FOVKickDown() : fov.FOVKickUp());
        }

        return speed;
    }
}
