using Godot;
using Godot.Collections;
using System;
using System.Linq;

public class PointInfo
{
	public bool IsFallTile;
	public bool IsLeftEdge;
	public bool IsRightEdge;
	public bool IsLeftWall;
	public bool IsRightWall;
	public bool IsPositionPoint;
	public long PointID;
	public Vector2 Position;
	public PointInfo()
	{
	}
	public PointInfo(long pointID, Vector2 position)
	{
		PointID = pointID;
		Position = position;
	}
}

public partial class TileMapPathFind : TileMap
{
	[Export]
	public bool ShowDebugGraph = true;                  // If the graph points and lines should be drawn
	[Export]
	public int JumpDistance = 5;                       // Distance between two tiles to count as a jump
	[Export]
	public int JumpHeight = 4;                         // Height between two tiles to connect a jump	
	private const int COLLISION_LAYER = 0;              // The collision layer for the tiles
	private const int CELL_IS_EMPTY = -1;               // TileMap defines an empty space as -1
	private const int MAX_TILE_FALL_SCAN_DEPTH = 500;   // Max number of tiles to scand downwards for a solid tile

	private AStar2D _astarGraph = new AStar2D();        // The a star graph
	private Array<Vector2I> _usedTiles;                 // The used tiles in the TileMap
	private PackedScene _graphpoint;                    // The graph point node to visualize path

	private System.Collections.Generic.List<PointInfo> _pointInfoList = new System.Collections.Generic.List<PointInfo>();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Load in the graph point packed scene
		_graphpoint = ResourceLoader.Load<PackedScene>("res://Scenes/TileMapPathFind/GraphPoint.tscn");
		// Get all the used tiles in the tile map
		_usedTiles = GetUsedCells(COLLISION_LAYER);
		// Build the graph
		BuildGraph();
	}
	private void BuildGraph()
	{
		AddGraphPoints();   // Add all the grah points		

		// If the debug graph should not be shown
		if (!ShowDebugGraph)
		{
			ConnectPoints();    // Connect the points
		}
	}


	private PointInfo GetPointInfoAtPosition(Vector2 position)
	{
		var newInfoPoint = new PointInfo(-10000, position);     // Create a new PointInfo with the position
		newInfoPoint.IsPositionPoint = true;                    // Mark it as a position point
		var tile = LocalToMap(position);                        // Get the tile position		

		// If a tile is found below
		if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X, tile.Y + 1)) != CELL_IS_EMPTY)
		{
			// If a tile exist to the left
			if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X - 1, tile.Y)) != CELL_IS_EMPTY)
			{
				newInfoPoint.IsLeftWall = true;   // Flag that it's a left wall
			}
			// If a tile exist to the right
			if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X - 1, tile.Y)) != CELL_IS_EMPTY)
			{
				newInfoPoint.IsRightWall = true;  // Flag that it's a right wall
			}
			// If a tile doesn't exist one tile below to the left
			if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X - 1, tile.Y + 1)) != CELL_IS_EMPTY)
			{
				newInfoPoint.IsLeftEdge = true;  // Flag that it's a left edge
			}
			// If a tile doesn't exist one tile below to the right
			if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X + 1, tile.Y + 1)) != CELL_IS_EMPTY)
			{
				newInfoPoint.IsRightEdge = true;  // Flag that it's a right edge
			}
		}
		return newInfoPoint;
	}

	private System.Collections.Generic.Stack<PointInfo> ReversePathStack(System.Collections.Generic.Stack<PointInfo> pathStack)
	{
		System.Collections.Generic.Stack<PointInfo> pathStackReversed = new System.Collections.Generic.Stack<PointInfo>();
		// Reverse the path stack. It is like emptying a bucket of stones from one bucket to another. 
		// The stones at the top, will be in the bottom of the other bucket.
		while (pathStack.Count != 0)
		{
			pathStackReversed.Push(pathStack.Pop());
		}
		return pathStackReversed;
	}

	public System.Collections.Generic.Stack<PointInfo> GetPlaform2DPath(Vector2 startPos, Vector2 endPos)
	{
		System.Collections.Generic.Stack<PointInfo> pathStack = new System.Collections.Generic.Stack<PointInfo>();
		// Find the path between the start and end position
		var idPath = _astarGraph.GetIdPath(_astarGraph.GetClosestPoint(startPos), _astarGraph.GetClosestPoint(endPos));

		if (idPath.Count() <= 0) { return pathStack; }      // If the the path has reached its goal, return the empty path stack

		var startPoint = GetPointInfoAtPosition(startPos);  // Create the point for the start position		
		var endPoint = GetPointInfoAtPosition(endPos);      // Create the point for the end position		
		var numPointsInPath = idPath.Count();               // Get number of points in the astar path

		// loop through all the points in the path
		for (int i = 0; i < numPointsInPath; ++i)
		{
			var currPoint = GetInfoPointByPointId(idPath[i]);   // Get the current point in the idPath		

			// If there's only one point in the path
			if (numPointsInPath == 1)
			{
				continue;   // Skip the point in the aStar path, the end point will be added as the only path point at the end.
			}
			// If it's the first point in the astar path
			if (i == 0 && numPointsInPath >= 2)
			{
				// Get the next second path point in the astar path
				var secondPathPoint = GetInfoPointByPointId(idPath[i + 1]);

				// If the start point is closer to the second path point than the current point
				if (startPoint.Position.DistanceTo(secondPathPoint.Position) < currPoint.Position.DistanceTo(secondPathPoint.Position))
				{
					pathStack.Push(startPoint); // Add the start point to the path
					continue;                   // Skip adding the current point and go to the next point in the path
				}
			}
			// If it's the last point in the path 
			else if (i == numPointsInPath - 1 && numPointsInPath >= 2)
			{
				// Get the penultimate point in the astar path list
				var penultimatePoint = GetInfoPointByPointId(idPath[i - 1]);

				// If the endPoint is closer than the last point in the astar path
				if (endPoint.Position.DistanceTo(penultimatePoint.Position) < currPoint.Position.DistanceTo(penultimatePoint.Position))
				{
					continue;                   // Skip addig the last point to the path stack
				}
				// If the last point is closer
				else
				{
					pathStack.Push(currPoint);  // Add the current point to the path stack
					break;                      // Break out of the for loop
				}
			}

			pathStack.Push(currPoint);      // Add the current point			
		}
		pathStack.Push(endPoint);           // Add the end point to the path		
		return ReversePathStack(pathStack); // Return the pathstack reversed		
	}

	private PointInfo GetInfoPointByPointId(long pointId)
	{
		// Find and return the first point in the _pointInfoList with the given pointId
		return _pointInfoList.Where(p => p.PointID == pointId).FirstOrDefault();
	}

	private void DrawDebugLine(Vector2 to, Vector2 from, Color color)
	{
		// If the debug graph should be visible
		if (ShowDebugGraph)
		{
			DrawLine(to, from, color); // Draw a line between the points with te given color
		}
	}

	private void AddGraphPoints()
	{
		// Loop through all the used tiles in the tilemap
		foreach (var tile in _usedTiles)
		{
			AddLeftEdgePoint(tile);
			AddRightEdgePoint(tile);
			AddLeftWallPoint(tile);
			AddRightWallPoint(tile);
			AddFallPoint(tile);
		}
	}
	public long TileAlreadyExistInGraph(Vector2I tile)
	{
		var localPos = MapToLocal(tile);                            // Map the position to screen coordiantes

		// If the graph contains points
		if (_astarGraph.GetPointCount() > 0)
		{
			var pointId = _astarGraph.GetClosestPoint(localPos);    // Find the closest point in the graph

			// If the points have the same local coordinates
			if (_astarGraph.GetPointPosition(pointId) == localPos)
			{
				return pointId;                                     // Return the point id, the tile already exist
			}
		}
		// if the node was n found, return -1
		return -1;
	}

	private void AddVisualPoint(Vector2I tile, Color? color = null, float scale = 1.0f)
	{
		// If the graph should not be shown, return out of the method
		if (!ShowDebugGraph) { return; }

		// Instantiate a new visual point
		Sprite2D visualPoint = _graphpoint.Instantiate() as Sprite2D;

		// If a custom color has been passed in
		if (color != null)
		{
			visualPoint.Modulate = (Color)color;    // Change the color of the visual point to the custom color
		}
		// If a custom scale has been passed in, and it is within valid range
		if (scale != 1.0f && scale > 0.1f)
		{
			visualPoint.Scale = new Vector2(scale, scale);  // Update the visual point scale
		}
		visualPoint.Position = MapToLocal(tile);    // Map the position of the visual point to local coordinates
		AddChild(visualPoint);                      // Add the visual point as a child to the scene
	}

	private PointInfo GetPointInfo(Vector2I tile)
	{
		// Loop through the point info list
		foreach (var pointInfo in _pointInfoList)
		{
			// If the tile has been added to the points list
			if (pointInfo.Position == MapToLocal(tile))
			{
				return pointInfo;   // Return the PointInfo
			}
		}
		return null; // If the tile wasn't found, return null
	}
	public override void _Draw()
	{
		// If the debug graph should be visible
		if (ShowDebugGraph)
		{
			ConnectPoints();    // Connect the points & draw the graph and its connections
		}
	}

	#region Connect Graph Points
	private void ConnectPoints()
	{
		// Loop through all the points in the point info list
		foreach (var p1 in _pointInfoList)
		{
			ConnectHorizontalPoints(p1);    // Connect the horizontal points in the graph			
			ConnectJumpPoints(p1);          // Connect the jump points in the graph	
			ConnectFallPoint(p1);           // Connect the fall points in the graph					
		}
	}

	private void ConnectFallPoint(PointInfo p1)
	{
		if (p1.IsLeftEdge || p1.IsRightEdge)
		{
			var tilePos = LocalToMap(p1.Position);
			// FindFallPoint expects the exact tile coordinate. The points in the graph is one tile above: y-1			
			// Therefore we adjust the y position with: Y += 1
			tilePos.Y += 1;

			Vector2I? fallPoint = FindFallPoint(tilePos);
			if (fallPoint != null)
			{
				var pointInfo = GetPointInfo((Vector2I)fallPoint);
				Vector2 p2Map = LocalToMap(p1.Position);
				Vector2 p1Map = LocalToMap(pointInfo.Position);

				if (p1Map.DistanceTo(p2Map) <= JumpHeight)
				{
					_astarGraph.ConnectPoints(p1.PointID, pointInfo.PointID);                       // Connect the points
					DrawDebugLine(p1.Position, pointInfo.Position, new Color(0, 1, 0, 1));          // Draw a Green line between the points
				}
				else
				{
					_astarGraph.ConnectPoints(p1.PointID, pointInfo.PointID, bidirectional: false);  // Only allow edge -> fallTile direction
					DrawDebugLine(p1.Position, pointInfo.Position, new Color(1, 1, 0, 1));          // Draw a yellow line between the points									
				}
			}
		}
	}

	private void ConnectJumpPoints(PointInfo p1)
	{
		foreach (var p2 in _pointInfoList)
		{
			ConnectHorizontalPlatformJumps(p1, p2);
			ConnectDiagonalJumpRightEdgeToLeftEdge(p1, p2);
			ConnectDiagonalJumpLeftEdgeToRightEdge(p1, p2);
		}
	}
	private void ConnectDiagonalJumpRightEdgeToLeftEdge(PointInfo p1, PointInfo p2)
	{
		if (p1.IsRightEdge)
		{
			Vector2 p1Map = LocalToMap(p1.Position);
			Vector2 p2Map = LocalToMap(p2.Position);

			if (p2.IsLeftEdge                                                   // If the p2 tile is a right edge
			&& p2.Position.X > p1.Position.X                                    // And the p2 tile is to the right of the p1 tile
			&& p2.Position.Y > p1.Position.Y                                    // And the p2 tile is below the p1 tile
			&& p2Map.DistanceTo(p1Map) < JumpDistance)                          // And the distance between the p2 and p1 map position is within jump reach
			{
				_astarGraph.ConnectPoints(p1.PointID, p2.PointID);              // Connect the points
				DrawDebugLine(p1.Position, p2.Position, new Color(0, 1, 0, 1)); // Draw a green line between the points
			}
		}
	}

	private void ConnectDiagonalJumpLeftEdgeToRightEdge(PointInfo p1, PointInfo p2)
	{
		if (p1.IsLeftEdge)
		{
			Vector2 p1Map = LocalToMap(p1.Position);
			Vector2 p2Map = LocalToMap(p2.Position);
			if (p2.IsRightEdge                                                  // If the p2 tile is a right edge
			&& p2.Position.X < p1.Position.X                                    // and the p2 tile is to the left of the p1 tile
			&& p2.Position.Y > p1.Position.Y                                    // and the p2 tile is below the p1 tile
			&& p2Map.DistanceTo(p1Map) < JumpDistance)                          // And the distance between the p2 and p1 map position is within jump reach
			{
				_astarGraph.ConnectPoints(p1.PointID, p2.PointID);              // Connect the points
				DrawDebugLine(p1.Position, p2.Position, new Color(0, 1, 0, 1)); // Draw a green line between the points
			}
		}
	}


	private void ConnectHorizontalPlatformJumps(PointInfo p1, PointInfo p2)
	{
		if (p1.PointID == p2.PointID) { return; } // If the points are the same, return out of the method

		// If the points are on the same height and p1 is a right edge, and p2 is a left edge	
		if (p2.Position.Y == p1.Position.Y && p1.IsRightEdge && p2.IsLeftEdge)
		{
			// If the p2 position is to the right of the p1 position
			if (p2.Position.X > p1.Position.X)
			{
				Vector2 p2Map = LocalToMap(p2.Position);    // Get the p2 tile position
				Vector2 p1Map = LocalToMap(p1.Position);    // Get the p1 tile position				

				// If the distance between the p2 and p1 map position are within jump reach
				if (p2Map.DistanceTo(p1Map) < JumpDistance + 1)
				{
					_astarGraph.ConnectPoints(p1.PointID, p2.PointID);              // Connect the points
					DrawDebugLine(p1.Position, p2.Position, new Color(0, 1, 0, 1)); // Draw a green line between the points
				}
			}
		}
	}
	private void ConnectHorizontalPoints(PointInfo p1)
	{
		if (p1.IsLeftEdge || p1.IsLeftWall || p1.IsFallTile)
		{
			PointInfo closest = null;

			// Loop through the point info list
			foreach (var p2 in _pointInfoList)
			{
				if (p1.PointID == p2.PointID) { continue; } // If the points are the same, go to the next point

				// If the point is a right edge or a right wall, and the height (Y position) is the same, and the p2 position is to the right of the p1 point
				if ((p2.IsRightEdge || p2.IsRightWall || p2.IsFallTile) && p2.Position.Y == p1.Position.Y && p2.Position.X > p1.Position.X)
				{
					// If the closest point has not yet been initialized
					if (closest == null)
					{
						closest = new PointInfo(p2.PointID, p2.Position);   // Initialize it to the p2 point
					}
					// If the p2 point is closer than the current closest point
					if (p2.Position.X < closest.Position.X)
					{
						closest.Position = p2.Position; // Update the closest point position
						closest.PointID = p2.PointID;   // Update the pointId
					}
				}
			}
			// If a closest point was found
			if (closest != null)
			{
				// If a horizontal connection cannot be made
				if (!HorizontalConnectionCannotBeMade((Vector2I)p1.Position, (Vector2I)closest.Position))
				{
					_astarGraph.ConnectPoints(p1.PointID, closest.PointID);                 // Connect the points
					DrawDebugLine(p1.Position, closest.Position, new Color(0, 1, 0, 1));    // Draw a green line between the points
				}
			}
		}
	}
	private bool HorizontalConnectionCannotBeMade(Vector2I p1, Vector2I p2)
	{
		// Convert the position to tile coordinates
		Vector2I startScan = LocalToMap(p1);
		Vector2I endScan = LocalToMap(p2);

		// Loop through all tiles between the points
		for (int i = startScan.X; i < endScan.X; ++i)
		{
			if (GetCellSourceId(COLLISION_LAYER, new Vector2I(i, startScan.Y)) != CELL_IS_EMPTY         // If the cell is not empty (a wall)
			|| GetCellSourceId(COLLISION_LAYER, new Vector2I(i, startScan.Y + 1)) == CELL_IS_EMPTY)     // or the cell below is empty (an edge tile)
			{
				return true;    // Return true, the connection cannot be made
			}
		}
		return false;
	}

	#endregion

	#region Tile Fall Points
	private Vector2I? GetStartScanTileForFallPoint(Vector2I tile)
	{
		var tileAbove = new Vector2I(tile.X, tile.Y - 1);
		var point = GetPointInfo(tileAbove);

		// If the point did not exist in the point info list
		if (point == null) { return null; }  // Return null

		var tileScan = Vector2I.Zero;

		// If the point is a left edge
		if (point.IsLeftEdge)
		{
			tileScan = new Vector2I(tile.X - 1, tile.Y - 1);    // Set the start position to start scanning one tile to the left
			return tileScan;                                    // Return the tile scan position
		}
		// If the point is a right edge
		else if (point.IsRightEdge)
		{
			tileScan = new Vector2I(tile.X + 1, tile.Y - 1);    // Set the start position to start scanning one tile to the left
			return tileScan;                                    // Return the tile scan position
		}
		return null;    // Return null			
	}

	private Vector2I? FindFallPoint(Vector2 tile)
	{
		var scan = GetStartScanTileForFallPoint((Vector2I)tile);// Get the start scan tile position
		if (scan == null) { return null; }                      // If it wasn't found, return out of the method

		var tileScan = (Vector2I)scan;                          // Typecast nullable Vector2I? to Vector2I
		Vector2I? fallTile = null;                              // Initialize the falltile to null

		// Loop, and start to look for a solid tile
		for (int i = 0; i < MAX_TILE_FALL_SCAN_DEPTH; ++i)
		{
			// If the tile cell below is solid
			if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tileScan.X, tileScan.Y + 1)) != CELL_IS_EMPTY)
			{
				fallTile = tileScan;    // The fall tile was found
				break;                  // Break out of the for loop
			}
			// If a solid tile was not found, scan the next tile below the current one
			tileScan.Y++;
		}
		return fallTile;    // return the fall tile result
	}

	private void AddFallPoint(Vector2I tile)
	{
		Vector2I? fallTile = FindFallPoint(tile);                                           // Find the fall tile point
		if (fallTile == null) { return; }                                                   // If the fall tile was not found, return out of the method
		var fallTileLocal = (Vector2I)MapToLocal((Vector2I)fallTile);                       // Get the local coordinates for the fall tile

		long existingPointId = TileAlreadyExistInGraph((Vector2I)fallTile);                 // Check if the point already has been added

		// If the tile doesn't exist in the graph already
		if (existingPointId == -1)
		{
			long pointId = _astarGraph.GetAvailablePointId();                               // Get the next available point id
			var pointInfo = new PointInfo(pointId, fallTileLocal);                          // Create point information, and pass in the pointId and tile
			pointInfo.IsFallTile = true;                                                    // Flag that the tile is a fall tile
			_pointInfoList.Add(pointInfo);                                                  // Add the tile to the point info list
			_astarGraph.AddPoint(pointId, fallTileLocal);                                   // Add the point to the Astar graph, in local coordinates
			AddVisualPoint((Vector2I)fallTile, new Color(1, 0.35f, 0.1f, 1), scale: 0.35f); // Add the point visually to the map (if ShowDebugGraph = true)
		}
		else
		{
			_pointInfoList.Single(x => x.PointID == existingPointId).IsFallTile = true;     // flag that it's a fall point			
			var updateInfo = _pointInfoList.Find(x => x.PointID == existingPointId);        // Find the existing point info
			updateInfo.IsFallTile = true;                                                   // Flag that it's a fall tile				
			AddVisualPoint((Vector2I)fallTile, new Color("#ef7d57"), scale: 0.30f);         // Add the point visually to the map (if ShowDebugGraph = true)
		}
	}

	#endregion
	#region Tile Edge & Wall Graph Points
	private void AddLeftEdgePoint(Vector2I tile)
	{
		// If a tile exist above, it's not an edge
		if (TileAboveExist(tile))
		{
			return;   // Return out of the method
		}
		// If the tile to the left (X - 1) is empty
		if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X - 1, tile.Y)) == CELL_IS_EMPTY)
		{
			var tileAbove = new Vector2I(tile.X, tile.Y - 1);                           // The graph points to follow, are one tile above the ground			

			long existingPointId = TileAlreadyExistInGraph(tileAbove);                  // Check if the point already has been added		
																						// If the point has not already been added
			if (existingPointId == -1)
			{
				long pointId = _astarGraph.GetAvailablePointId();                       // Get the next available point id
				var pointInfo = new PointInfo(pointId, (Vector2I)MapToLocal(tileAbove));// Create a new point information, and pass in the pointId
				pointInfo.IsLeftEdge = true;                                            // Flag that the tile is a left edge
				_pointInfoList.Add(pointInfo);                                          // Add the tile to the point info list
				_astarGraph.AddPoint(pointId, (Vector2I)MapToLocal(tileAbove));         // Add the point to the Astar graph, in local coordinates
				AddVisualPoint(tileAbove);                                              // Add the point visually to the map (if ShowDebugGraph = true)				
			}
			else
			{
				_pointInfoList.Single(x => x.PointID == existingPointId).IsLeftEdge = true;    // flag that it's a left edge
				AddVisualPoint(tileAbove, new Color("#73eff7"));               // Add the point visually to the map					
			}
		}
	}
	private void AddRightEdgePoint(Vector2I tile)
	{
		// If a tile exist above, it's not an edge
		if (TileAboveExist(tile))
		{
			return;   // Return out of the method
		}
		// If the tile to the right (X + 1) is empty
		if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X + 1, tile.Y)) == CELL_IS_EMPTY)
		{
			var tileAbove = new Vector2I(tile.X, tile.Y - 1);                           // The graph points to follow, are one tile above the ground			

			long existingPointId = TileAlreadyExistInGraph(tileAbove);                  // Check if the point already has been added		
																						// If the point has not already been added
			if (existingPointId == -1)
			{
				long pointId = _astarGraph.GetAvailablePointId();                       // Get the next available point id
				var pointInfo = new PointInfo(pointId, (Vector2I)MapToLocal(tileAbove));// Create a new point information, and pass in the pointId
				pointInfo.IsRightEdge = true;                                           // Flag that the tile is a right edge
				_pointInfoList.Add(pointInfo);                                          // Add the tile to the point info list
				_astarGraph.AddPoint(pointId, (Vector2I)MapToLocal(tileAbove));         // Add the point to the Astar graph, in local coordinates
				AddVisualPoint(tileAbove, new Color("#94b0c2"));                            // Add the point visually to the map (if ShowDebugGraph = true)			
			}
			else
			{
				_pointInfoList.Single(x => x.PointID == existingPointId).IsRightEdge = true; // flag that it's a left edge
				AddVisualPoint(tileAbove, new Color("#ffcd75"));                             // Add the point visually to the map (if ShowDebugGraph = true)			
			}
		}
	}

	private void AddLeftWallPoint(Vector2I tile)
	{
		// If a tile exist above, it's not an edge
		if (TileAboveExist(tile))
		{
			return;   // Return out of the method
		}
		// If the tile to the up-left (X - 1, Y -1) is not empty
		if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X - 1, tile.Y - 1)) != CELL_IS_EMPTY)
		{
			var tileAbove = new Vector2I(tile.X, tile.Y - 1);                           // The graph points to follow, are one tile above the ground			

			long existingPointId = TileAlreadyExistInGraph(tileAbove);                  // Check if the point already has been added		
																						// If the point has not already been added
			if (existingPointId == -1)
			{
				long pointId = _astarGraph.GetAvailablePointId();                       // Get the next available point id
				var pointInfo = new PointInfo(pointId, (Vector2I)MapToLocal(tileAbove));// Create a new point information, and pass in the pointId
				pointInfo.IsLeftWall = true;                                            // Flag that the tile is a left wall	
				_pointInfoList.Add(pointInfo);                                          // Add the tile to the point info list
				_astarGraph.AddPoint(pointId, (Vector2I)MapToLocal(tileAbove));         // Add the point to the Astar graph, in local coordinates
				AddVisualPoint(tileAbove, new Color(0, 0, 0, 1));                       // Add a black point to the map (if ShowDebugGraph = true)
			}
			else
			{
				_pointInfoList.Single(x => x.PointID == existingPointId).IsLeftWall = true; // flag that it's a left edge
				AddVisualPoint(tileAbove, new Color(0, 0, 1, 1), 0.45f);                    // Add a blue small point to the same location on map
			}
		}
	}

	private void AddRightWallPoint(Vector2I tile)
	{
		// If a tile exist above, it's not an edge
		if (TileAboveExist(tile))
		{
			return;   // Return out of the method
		}
		// If the tile to the up-right (X + 1, Y -1) is not empty
		if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X + 1, tile.Y - 1)) != CELL_IS_EMPTY)
		{
			var tileAbove = new Vector2I(tile.X, tile.Y - 1);                           // The graph points to follow, are one tile above the ground			

			long existingPointId = TileAlreadyExistInGraph(tileAbove);                  // Check if the point already has been added		
																						// If the point has not already been added
			if (existingPointId == -1)
			{
				long pointId = _astarGraph.GetAvailablePointId();                       // Get the next available point id
				var pointInfo = new PointInfo(pointId, (Vector2I)MapToLocal(tileAbove));// Create a new point information, and pass in the pointId
				pointInfo.IsRightWall = true;                                               // Flag that the tile is a right wall	
				_pointInfoList.Add(pointInfo);                                          // Add the tile to the point info list
				_astarGraph.AddPoint(pointId, (Vector2I)MapToLocal(tileAbove));         // Add the point to the Astar graph, in local coordinates
				AddVisualPoint(tileAbove, new Color(0, 0, 0, 1));                       // Add a black point to the map (if ShowDebugGraph = true)
			}
			else
			{
				_pointInfoList.Single(x => x.PointID == existingPointId).IsLeftEdge = true; // flag that it's a left edge
				AddVisualPoint(tileAbove, new Color("566c86"), 0.65f);                      // Add a purple small point to the same location on map
			}
		}
	}

	private bool TileAboveExist(Vector2I tile)
	{
		// If a tile doesn't exist above (Y - 1)
		if (GetCellSourceId(COLLISION_LAYER, new Vector2I(tile.X, tile.Y - 1)) == CELL_IS_EMPTY)
		{
			return false;   // If it's empty, return false
		}
		return true;
	}
	#endregion

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
