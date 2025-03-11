using UnityEngine;

public class deleteAfter : MonoBehaviour
{
    public float mouseSensitivity = 300f;
    private float xRotation = 0f;
    private float yRotation = 0f;
    public float topClamp = -90f;
    public float bottonClamp = 70f;
    public float movementSpeed = 5.0f; // Скорость движения
    public float jumpForce = 5.0f; // Сила прыжка
    public float gravity = -9.81f; // Гравитация
    public Animator animator;
    public float smoothmouse;
    private CharacterController controller;
    public Transform playerCamera;
    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        animator.SetFloat("X_movement", Input.GetAxis("Horizontal"));
        animator.SetFloat("Y_movement", Input.GetAxis("Vertical"));
        smoothmouse = Mathf.Lerp(smoothmouse, Input.GetAxis("Mouse X"), Time.deltaTime * 10);
        animator.SetFloat("X_mouse", smoothmouse);

        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical"); 

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * movementSpeed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, topClamp, bottonClamp);
        yRotation += mouseX;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        playerCamera.rotation = Quaternion.Euler(xRotation, yRotation, 0f);

    }
}

