using System.Runtime.CompilerServices;
using Sandbox;
using Sandbox.Citizen;

public sealed class PlayerController : Component
{
	[Property] public CharacterController CharacterController { get; set; }
	[Property] public int WalkSpeed { get; set; } = 100;
	[Property] public int RunSpeed { get; set; } = 200;
	[Property] public int CrouchSpeed { get; set; } = 50;
	[Sync] public Vector3 WishVelocity { get; set; }
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }
	[Property] public GameObject Hold { get; set; }
	[Sync] public bool IsFirstPerson { get; set; } = true;
	[Sync] public bool IsCrouching { get; set; }
	[Sync] public Angles eyeAngles { get; set; }
	[Sync] public bool IsGrabbing { get; set; } = false;
	[Property] public Interactor Interactor { get; set; }
	[Sync] public float Health { get; set; } = 100;
	

	public Item CurrentItem;
	public Inventory Inventory;

	protected override void OnStart()
	{
		
	}
	private void MouseInput()
	{
		var e = eyeAngles;
		e += Input.AnalogLook;
		e.pitch = e.pitch.Clamp( -85, 90 );
		e.roll = 0.0f;
		eyeAngles = e;
	}
	protected override void OnUpdate()
	{
		UpdateAnimation();
		if (!IsProxy)
		{
			MouseInput();
			Movement();
			Crouch();
			//UpdateBodyShit();
			Transform.Rotation = Rotation.Slerp(Transform.Rotation, new Angles(0, eyeAngles.yaw, 0).ToRotation(), Time.Delta * 5);
		}
	}
	float MoveSpeed
	{
		get
		{
			if (IsCrouching)
			{
				return CrouchSpeed;
			}
			if (Input.Down("run"))
			{
				return RunSpeed;
			}
			else
			{
				return WalkSpeed;
			}
		}
	}
	float Friction()
	{
			if (CharacterController.IsOnGround)
			{
				return 6.0f;
			}
			else
			{
				return 0.2f;
			}
	}
	RealTimeSince timeSinceJump = 0;
	RealTimeSince timeSinceGround = 0;
	void Movement()
	{
		var cc = CharacterController;
		if (cc is null)
		{
			return;
		}
		WishVelocity = Input.AnalogMove;
		Vector3 halfGrav = Scene.PhysicsWorld.Gravity * Time.Delta * 0.5f;
		if (Input.Down("jump") && cc.IsOnGround && timeSinceJump > 0.1f)
		{
			CharacterController.Punch(Vector3.Up * 300);
			AnimationHelper.TriggerJump();
			timeSinceJump = 0;
		}

	if (!WishVelocity.IsNearlyZero())
	{
		WishVelocity = new Angles(0, eyeAngles.yaw, 0).ToRotation() * WishVelocity;
		WishVelocity = WishVelocity.WithZ(0);
		WishVelocity.ClampLength(1);
		WishVelocity *= MoveSpeed;
		if (!cc.IsOnGround)
		{
			WishVelocity.ClampLength(50);
		}
	}
	cc.ApplyFriction(Friction());

	if (cc.IsOnGround)
	{
		cc.Accelerate(WishVelocity);
		cc.Velocity = CharacterController.Velocity.WithZ( 0 );
	}
	else
	{
		cc.Velocity += halfGrav;
		cc.Accelerate(WishVelocity);
	}
	CharacterController.Move();
	if (!cc.IsOnGround)
	{
		cc.Velocity += halfGrav;
	}
	else
	{
		cc.Velocity = cc.Velocity.WithZ(0);
	}
	if (!cc.IsOnGround)
	{
		timeSinceGround = 0;
	}
	}
	bool UnCrouch()
	{
		if (!IsCrouching)
		{
			return true;
		}
		var tr = CharacterController.TraceDirection(Vector3.Up * 32);
		return !tr.Hit;
	}
	void CamPos()
	{
		var camera = Scene.GetAllComponents<CameraComponent>().Where(x => x.IsMainCamera).FirstOrDefault();
		if (camera is null) return;
		if (IsFirstPerson)
		{
		var targetPosEyePos = IsCrouching ? 32 : 64;
		var targetPos = Transform.Position + new Vector3(0, 0, targetPosEyePos);
		camera.Transform.Position = targetPos;
		camera.Transform.Rotation = eyeAngles;
		}
		else
		{
			  var lookDir = eyeAngles.ToRotation();
		//Set the camera rotation
        camera.Transform.Rotation = lookDir;
		var center = Transform.Position + Vector3.Up * 64;
		//Trace to see if the camera is inside a wall
		var tr = Scene.Trace.Ray(center, center - (eyeAngles.Forward * 300)).WithoutTags("player", "barrier").Run();
		if (tr.Hit)
		{
			camera.Transform.Position = tr.EndPosition + tr.Normal * 2 + Vector3.Up * 10;
		}
		else
		{
			camera.Transform.Position = center - (eyeAngles.Forward * 300) + Vector3.Up * 10;
		}
		}		
	}
	void Crouch()
	{
		if (!Input.Down("duck"))
		{
			if (!UnCrouch()) return;
			CharacterController.Height = 64;
			IsCrouching = false;
		}
		else
		{
			CharacterController.Height = 32;
			IsCrouching = true;
		}
	}
	protected override void OnPreRender()
	{
		UpdateBodyShit();

		if ( IsProxy )
			return;

		CamPos();
	}
	private void UpdateAnimation()
	{
		if ( AnimationHelper is null ) return;

		var wv = WishVelocity.Length;

		AnimationHelper.WithWishVelocity( WishVelocity );
		AnimationHelper.WithVelocity( CharacterController.Velocity );
		AnimationHelper.IsGrounded = CharacterController.IsOnGround;
		AnimationHelper.DuckLevel = IsCrouching ? 1.0f : 0.0f;

		AnimationHelper.MoveStyle = wv < 160f ? CitizenAnimationHelper.MoveStyles.Walk : CitizenAnimationHelper.MoveStyles.Run;

		var lookDir = eyeAngles.ToRotation().Forward * 1024;
		AnimationHelper.WithLook( lookDir, 1, 0.5f, 0.25f );
	}
	public void UpdateBodyShit()
	{
		var target = AnimationHelper.Target;
		var cloths = target.Components.GetAll<ModelRenderer>(FindMode.InChildren);
		if (IsProxy || !IsFirstPerson)
		{
			target.RenderType = ModelRenderer.ShadowRenderType.On;
			foreach (var cloth in cloths)
			{
				cloth.RenderType = ModelRenderer.ShadowRenderType.On;
			}
		}
		else
		{
			target.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
			foreach (var cloth in cloths)
			{
				cloth.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
			}
		}
	}

	[Broadcast]
	public void TakeDamage(float damage)
	{
		if (IsProxy) return;
		Health -= damage;
	}

	[Broadcast]
	public void Heal(float amount)
	{
		if (IsProxy) return;
		Health += amount;
	}

	[ActionGraphNode("Take Damage Node"), Pure]
	public void TakeDamageNode(float damage)
	{
		TakeDamage(damage);
	}

	[ActionGraphNode("Heal Node"), Pure]
	public void HealNode(float amount)
	{
		Heal(amount);
	}
}