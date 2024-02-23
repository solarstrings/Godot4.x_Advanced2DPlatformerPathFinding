using Godot;
using System;
using System.Collections.Generic;

public partial class Skeleton : CharacterBody2D
{
	public const float Speed = 300.0f;
	public const float JumpVelocity = -500.0f;
	public const float SmallJumpVelocity = -390.0f;
	public const float TinyJumpVelocity = -290.0f;

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

	private TileMapPathFind _pathFind2D;
	private Stack<PointInfo> _path = new Stack<PointInfo>();
	private PointInfo _target = null;
	private PointInfo _prevTarget = null;
	private float JumpDistanceHeightThreshold = 120.0f;
	private Timer _startTime;
	private Player _player;
	public override void _Ready()
	{
		_pathFind2D = FindParent("Main").FindChild("TileMapPathFind") as TileMapPathFind;
		_player = FindParent("Main").FindChild("Player") as Player;
		_startTime = GetNode<Timer>("PathFindTimer");
	}

	private void OnPathFindTimerTimeout()
	{
		// if (IsOnFloor() && _player.IsOnFloor()) 	// <- This line of code
		// will ensure that the skeleton ner falls down between gaps,
		// but can introduce pauses to the skeleton if the player is in the air too much.

		// If the skeleton is on the floor
		if (IsOnFloor())
		{
			DoPathFinding();
		}
	}


	private void GoToNextPointInPath()
	{
		// If there's no points in the path
		if (_path.Count <= 0)
		{
			_prevTarget = null; // Set previous target to null
			_target = null;     // Set target to null
			return;             // Return out of the method
		}

		_prevTarget = _target;  // Set the previous target to the current target
		_target = _path.Pop();  // Set the target node to the next target in the stack
	}

	private void DoPathFinding()
	{
		var playerTilePosition = _pathFind2D.LocalToMap(_player.Position);
		_path = _pathFind2D.GetPlaform2DPath(this.Position, _pathFind2D.MapToLocal(playerTilePosition));
		GoToNextPointInPath();
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;
		Vector2 direction = Vector2.Zero;

		// Handle start hunt.
		if (Input.IsActionJustPressed("ui_accept"))
			_startTime.Start();

		// Add the gravity.
		if (!IsOnFloor())
			velocity.Y += gravity * (float)delta;

		// if there is a target set
		if (_target != null)
		{
			// If the target is to the right of the current pusition
			if (_target.Position.X - 5 > Position.X)
			{
				direction.X = 1f;
			}
			// If the target is to the left of the current pusition
			else if (_target.Position.X + 5 < Position.X)
			{
				direction.X = -1f;
			}
			else
			{
				if (IsOnFloor())
				{
					GoToNextPointInPath();
					Jump(ref velocity);
				}
			}
		}

		if (direction != Vector2.Zero)
		{
			velocity.X = direction.X * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	private bool JumpRightEdgeToLeftEdge()
	{
		// If the previous tile was a right edge, and the target tile is a left edge
		if (_prevTarget.IsRightEdge && _target.IsLeftEdge
		&& _prevTarget.Position.Y <= _target.Position.Y // And the previous target is below the target tile
		&& _prevTarget.Position.X < _target.Position.X) // And the previous target is to the left of the target
		{
			return true;    // Return true, perform the jump
		}
		return false;       // return false, don't perform the jump
	}

	private bool JumpLeftEdgeToRightEdge()
	{
		// If the previous tile was a left edge, and the target tile is a right edge
		if (_prevTarget.IsLeftEdge && _target.IsRightEdge
		&& _prevTarget.Position.Y <= _target.Position.Y // And the previous target is below the target tile
		&& _prevTarget.Position.X > _target.Position.X) // And the previous target is to the right of the target
		{
			return true;    // Return true, perform the jump
		}
		return false;       // return false, don't perform the jump
	}
	private void Jump(ref Vector2 velocity)
	{
		if (_prevTarget == null || _target == null || _target.IsPositionPoint)
		{
			return;
		}

		// If the previous target is above the target, and the distance is less than the jump height threshold
		if (_prevTarget.Position.Y < _target.Position.Y
		&& _prevTarget.Position.DistanceTo(_target.Position) < JumpDistanceHeightThreshold)
		{
			return;
		}

		// If the current target is above the next target and the next target is a fall tile
		if (_prevTarget.Position.Y < _target.Position.Y && _target.IsFallTile)
		{
			return; // Return, no need to jump
		}

		if (_prevTarget.Position.Y > _target.Position.Y || JumpRightEdgeToLeftEdge() || JumpLeftEdgeToRightEdge())
		{
			int heightDistance = _pathFind2D.LocalToMap(_target.Position).Y - _pathFind2D.LocalToMap(_prevTarget.Position).Y;
			if (Mathf.Abs(heightDistance) <= 1)
			{
				velocity.Y = TinyJumpVelocity;
			}
			else if (Mathf.Abs(heightDistance) == 2)
			{
				velocity.Y = SmallJumpVelocity;
			}
			else
			{
				velocity.Y = JumpVelocity;
			}
		}
	}

}
