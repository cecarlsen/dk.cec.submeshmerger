/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SubmeshMerger
{
	public static class SubmeshMergerUtility
	{
		static Mesh _quadMesh;
		static Material _material;


		public static Mesh MergeMeshesAndSubmeshes( List<MeshFilter> meshFilters, Vector2Int uvGridDimensions )
		{
			Vector2 uvStep = new Vector2( 1f / uvGridDimensions.x, 1f / uvGridDimensions.y );
			MeshTopology topology = MeshTopology.Triangles;
			int t = 0;

			var meshMods = new Mesh[ meshFilters.Count ];
			for( int m = 0; m < meshFilters.Count; m++ )
			{
				var meshMod = Object.Instantiate( meshFilters[ m ].sharedMesh );
				meshMods[ m ] = meshMod;
				var originalUVs = meshMod.uv;
				var combinedUVs = new Vector2[ originalUVs.Length ];
				int submeshCount = meshMod.subMeshCount;
				
				uint combinedIndexCount = 0;
				for( int sm = 0; sm < submeshCount; sm++ ) combinedIndexCount += meshMod.GetIndexCount( sm );
				int[] combinedIndices = new int[ combinedIndexCount ];

				int i = 0;
				for( int sm = 0; sm < submeshCount; sm++ )
				{
					if( m == 0 && sm == 0 ) topology = meshMod.GetTopology( sm );
					else if( meshMod.GetTopology( sm ) != topology ) throw new System.Exception( "All meshes and submeshes must share same topology" );

					int column = t % uvGridDimensions.x;
					int row = t / uvGridDimensions.x;
					t++;
					var uvOffset = new Vector2( column * uvStep.x, row * uvStep.y );
					
					int[] submeshIndices = meshMod.GetIndices( sm, applyBaseVertex: true );
					for( int si = 0; si < submeshIndices.Length; si++ )
					{
						int index = submeshIndices[ si ];
						combinedIndices[ i++ ] = index;

						// UVs. It may be that we transform the same uv multiple times because triangles can share vertices, but I see no other way.
						var uv = originalUVs[ index ];
						uv = new Vector2( Mathf.Repeat( uv.x, 1f ), Mathf.Repeat( uv.y, 1f ) ); // In case UVs are lie beyond within 0-1 limits.
						combinedUVs[ index ] = new Vector2( uv.x * uvStep.x + uvOffset.x, uv.y * uvStep.y + uvOffset.y );
					}
				}
				meshMod.uv = combinedUVs;

				var combinedSubmeshDescriptor = new SubMeshDescriptor( indexStart: 0, indexCount: (int) combinedIndexCount, topology );
				meshMod.SetSubMeshes( new SubMeshDescriptor[]{ combinedSubmeshDescriptor }, MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds );
				meshMod.SetIndices( combinedIndices, topology, submesh: 0 );
			}

			if( meshFilters.Count == 1 ) return meshMods[ 0 ];

			var combines = new CombineInstance[ meshFilters.Count ];
			for( int m = 0; m < meshFilters.Count; m++ )
			{
				combines[ m ].mesh = meshMods[ m ];
				combines[ m ].transform = meshFilters[ m ].transform.localToWorldMatrix;
			}
			Mesh combinedMesh = new Mesh();
			combinedMesh.CombineMeshes( combines );

			return combinedMesh;
		}


		public static Texture2D MergeTexturesInGridLayout( Vector2Int resolution, Vector2Int gridDimensions, List<Texture2D> textures )
		{
			Texture2D resultTexture = new Texture2D( resolution.x, resolution.y );

			RenderTexture workTexture = new RenderTexture( resolution.x, resolution.y, 0 );

			int i = 0;
			Vector2 gridStep = new Vector2( 1f / gridDimensions.x, 1f / gridDimensions.y );
			for( int r = 0; r < gridDimensions.x; r++ )
				for( int c = 0; c < gridDimensions.y; c++ ){
				{
					if( i < textures.Count ){
						Rect destRect = new Rect( c * gridStep.x, r * gridStep.y, gridStep.x, gridStep.y );
						Copy( textures[ i++ ], new Rect( 0f, 0f, 1f, 1f ), workTexture, destRect );
					}
				}
			}

			RenderTexture.active = workTexture;
			resultTexture.ReadPixels( new Rect( 0, 0, resolution.x, resolution.y ), 0, 0 );
			resultTexture.Apply();
			RenderTexture.active = null;
			workTexture.Release();

			return resultTexture;
		}


		public static void Copy( Texture source, Rect sourceUVRect, RenderTexture target, Rect targetUVRect )
		{
			if( !source ) return;
			
			// Push.
			RenderTexture prevActive = RenderTexture.active;
			RenderTexture.active = target;
			
			// Execute.
			Material material = GetMaterial();
			material.SetTexture( ShaderIDs._MainTex, source );
			material.SetVector( ShaderIDs._UVTransform, new Vector4( sourceUVRect.x, sourceUVRect.y, sourceUVRect.width, sourceUVRect.height ) );
			material.SetVector( ShaderIDs._QuadTransform, new Vector4( targetUVRect.x, targetUVRect.y, targetUVRect.width, targetUVRect.height ) );
			material.SetPass( 0 );
			Graphics.DrawMeshNow( GetQuadMesh(), Matrix4x4.identity, 0 );
			target.IncrementUpdateCount();
			
			// Pop.
			RenderTexture.active = prevActive;
		}


		static class ShaderIDs
		{
			public static readonly int _MainTex = Shader.PropertyToID( nameof( _MainTex ) );
			public static readonly int _UVTransform = Shader.PropertyToID( nameof( _UVTransform ) );
			public static readonly int _QuadTransform = Shader.PropertyToID( nameof( _QuadTransform ) );
		}


		static Material GetMaterial()
		{
			if( !_material ) _material = new Material( Shader.Find( "Hidden/HiddenSubmeshMerger" ) );
			
			return _material;
		}

		static Mesh GetQuadMesh()
		{
			if( !_quadMesh )
			{
				_quadMesh = new Mesh { name = "Quad" };
				
				_quadMesh.SetVertices( new List<Vector3> // Notice these are unsigned.
				{
					new Vector3( 0f, 0f, 0f ),
					new Vector3( 1f, 0f, 0f ),
					new Vector3( 0f, 1f, 0f ),
					new Vector3( 1f, 0f, 0f ),
					new Vector3( 1f, 1f, 0f ),
					new Vector3( 0f, 1f, 0f ),
				});
				_quadMesh.SetIndices( new [] { 0, 1, 2, 3, 4, 5 }, MeshTopology.Triangles, 0, false );
				_quadMesh.UploadMeshData( true );
			}
			
			return _quadMesh;
		}
	}	
}