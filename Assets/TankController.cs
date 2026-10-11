using UnityEngine;

public class TankController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotateSpeed = 180f;
    private Rigidbody2D rb;
 
    private float moveInput;
    private float rotateInput;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // ��������� ������� ������ ������ ����
        moveInput = Input.GetAxisRaw("Vertical");    
        rotateInput = Input.GetAxisRaw("Horizontal"); // A / D ��� ������� �����/������
    }

    void FixedUpdate()
    {
        // �������� ������/����� ����� ������
        rb.linearVelocity = (Vector2)transform.up * moveInput * moveSpeed;

        // ������� ������� ����� Rigidbody2D
        if (rotateInput != 0)
        {
            float rotationAmount = -rotateInput * rotateSpeed * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation + rotationAmount);
        }
    }
}