/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk

	TODO
		- In window error feedback and better UX feedback in general.
*/

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditorInternal.Profiling.Memory.Experimental;

namespace SubmeshMerger
{
	public class SubmeshMergerWindow : EditorWindow
	{
		const float windowPadding = 5f;
		const float verticalSpacing = 5;


		[MenuItem("Tools/Submesh Merger")]
		public static void ShowExample()
		{
			var wnd = GetWindow<SubmeshMergerWindow>();
			wnd.titleContent = new GUIContent( "SubmeshMerger" );
		}


		public void CreateGUI()
		{
			var textureLookup = new Dictionary<string,List<Texture2D>>();
			var meshRenderers = new List<MeshRenderer>();
			var meshFilters = new List<MeshFilter>();

			var root = rootVisualElement;
			var view = new ScrollView(){
				mode = ScrollViewMode.Vertical
			};
			root.Add( view );

			// Create elements and styles.
			root.style.paddingLeft = new StyleLength( windowPadding );
			root.style.paddingRight = new StyleLength( windowPadding );
			root.style.paddingTop = new StyleLength( windowPadding );
			root.style.paddingBottom = new StyleLength( windowPadding );
			var objectLabel = new Label("Drop prefab asset here");
			var objectField = new ObjectField(){
				name = "modelAssetField",
				objectType = typeof( GameObject ),
			};
			var submeshContainer = new VisualElement();
			submeshContainer.SetEnabled( false );
			var textureResolutionLabel = new Label( "Output Texture Resolution" );
			var textureResolutionFieldContainer = new VisualElement();
			textureResolutionFieldContainer.style.flexDirection = FlexDirection.Row;
			var widthField = new EnumField();
			var heightField = new EnumField();
			widthField.Init( Resolution._16384 );
			heightField.Init( Resolution._16384 );
			var textureLayoutLabel = new Label( "Output Texture Grid Layout" );
			var textureLayoutFieldContainer = new VisualElement();
			textureLayoutFieldContainer.style.flexDirection = FlexDirection.Row;
			textureLayoutFieldContainer.style.width = new StyleLength( new Length( 100, LengthUnit.Percent ) );
			var textureColumnsField = new IntegerField(){ value = 3, };
			var textureRowsField = new IntegerField(){ value = 3, };
			var textureLayoutPreviewsContainer = new VisualElement();
			var button = new Button(){ text = "Merge!" };

			// Create hierarchy.
			view.Add( objectLabel );
			view.Add( objectField );
			view.Add( CreateVerticalSpacer() );
			view.Add( submeshContainer );
			view.Add( textureResolutionLabel );
			view.Add( textureResolutionFieldContainer );
			textureResolutionFieldContainer.Add( widthField );
			textureResolutionFieldContainer.Add( heightField );
			view.Add( CreateVerticalSpacer() );
			view.Add( textureLayoutLabel );
			view.Add( textureLayoutFieldContainer );
			textureLayoutFieldContainer.Add( textureColumnsField );
			textureLayoutFieldContainer.Add( textureRowsField );
			view.Add( CreateVerticalSpacer() );
			view.Add( textureLayoutPreviewsContainer );
			view.Add( CreateVerticalSpacer() );
			view.Add( button );

			// Register callbacks.
			Shader shader = null;
			objectField.RegisterValueChangedCallback( e =>
			{
				submeshContainer.Clear();
				textureLookup.Clear();

				// Collect valid components.
				var go = objectField.value as GameObject;
				var meshRendererCandidates = go.GetComponentsInChildren<MeshRenderer>();
				meshRenderers.Clear();
				meshFilters.Clear();
				foreach( var meshRendererCandidate in meshRendererCandidates ){
					if( !meshRendererCandidate.sharedMaterial ) continue;
					var meshFilterCandidate = meshRendererCandidate.gameObject.GetComponent<MeshFilter>();
					if( meshFilterCandidate && meshFilterCandidate.sharedMesh ){
						meshRenderers.Add( meshRendererCandidate );
						meshFilters.Add( meshFilterCandidate );
					}
				}

				int totalSubmeshCount = 0;
				for( int m = 0; m < meshRenderers.Count; m++ )
				{
					var meshRenderer = meshRenderers[ m ];
					var meshFilter = meshFilters[ m ];
					int expectedSubMeshCount = meshRenderer.sharedMaterials.Length;
					if( expectedSubMeshCount != meshFilter.sharedMesh.subMeshCount ) Debug.LogError( "Number of materials in MeshRenderer does not match number of submeshes in mesh." );
					
					for( int sm = 0; sm < expectedSubMeshCount; sm++ )
					{
						var materialContainer = new VisualElement();
						materialContainer.style.flexDirection = FlexDirection.Row;
						submeshContainer.Add( materialContainer );
						materialContainer.Add( new Label(){ text = $"Mesh { m }, Submesh { sm }" } );
						var material = meshRenderer.sharedMaterials[ sm ];
						if( sm == 0 ) shader = material.shader;
						else if( material.shader != shader ) throw new Exception( "All submeshes must share materials with same shader." );
						var materialField = new ObjectField(){
							objectType = typeof( Material ),
							value = material
						};
						materialContainer.Add( materialField );
						var textureProperyNames = material.GetTexturePropertyNames();
						for( int t = 0; t < textureProperyNames.Length; t++ )
						{
							var texturePropertyName = textureProperyNames[ t ];
							var texture = material.GetTexture( texturePropertyName ) as Texture2D;
							if( texture ){
								if( !textureLookup.ContainsKey( texturePropertyName ) ) textureLookup.Add( texturePropertyName, new List<Texture2D>() );
								var textureList = textureLookup[ texturePropertyName ];
								textureList.Add( texture );
								var textureContainer = new VisualElement();
								textureContainer.style.flexDirection = FlexDirection.Row;
								textureContainer.style.justifyContent = Justify.FlexEnd;
								textureContainer.Add( new Label(){ text = texturePropertyName } );
								var textureField = new ObjectField(){
									objectType = typeof( Texture ),
									value = texture,
								};
								textureField.style.width = 200;
								textureContainer.Add( textureField );
								submeshContainer.Add( textureContainer );
							}
						}
					}
					totalSubmeshCount += expectedSubMeshCount;
				}
				foreach( var data in textureLookup ){
					if( data.Value.Count != totalSubmeshCount ) Debug.LogError( $"Texture {data.Key} is not assigned for all materials. Count { data.Value.Count }, expected {totalSubmeshCount}." );
				}

				submeshContainer.Add( CreateVerticalSpacer() );
				GenerateTextureGridPreviews( textureLayoutPreviewsContainer, textureColumnsField.value, textureRowsField.value, textureLookup );
			});
			textureRowsField.RegisterValueChangedCallback( e => {
				GenerateTextureGridPreviews( textureLayoutPreviewsContainer, textureColumnsField.value, textureRowsField.value, textureLookup );
			});
			textureColumnsField.RegisterValueChangedCallback( e => {
				GenerateTextureGridPreviews( textureLayoutPreviewsContainer, textureColumnsField.value, textureRowsField.value, textureLookup );
			});
			button.clicked += () =>
			{
				if( textureLookup.Count == 0 ){
					Debug.LogError( "First add a valid object." );
				} else {
					var go = objectField.value as GameObject;
					Vector2Int gridDimensions = new Vector2Int( textureColumnsField.value, textureRowsField.value );
					string assetDatabaseDirectoryPath = Path.GetDirectoryName( AssetDatabase.GetAssetPath( go ) ) + "_Merged";
					string directoryPath = Directory.GetParent( Application.dataPath ) + "/" + assetDatabaseDirectoryPath;

					// Create directory.
					if( !Directory.Exists( directoryPath ) ) Directory.CreateDirectory( directoryPath );

					// Create and store texture.
					Vector2Int resolution = new Vector2Int( int.Parse( widthField.value.ToString().Substring( 1 ) ), int.Parse( heightField.value.ToString().Substring( 1 ) ) );
					var texturePropAndAssetLookup = new List<(string,Texture2D)>();
					string baseName = go.name + "_Merged";
					foreach( var data in textureLookup )
					{
						var texture = SubmeshMergerUtility.MergeTexturesInGridLayout( resolution, gridDimensions, data.Value );

						// Store texture.
						string textureFileName = baseName + data.Key + ".jpg";
						string textureFilePath = directoryPath + "/" + textureFileName;
						string textureAssetDatabaseFilePath = assetDatabaseDirectoryPath + "/" + textureFileName;
						File.WriteAllBytes( textureFilePath, texture.EncodeToJPG( quality: 95 ) );
						AssetDatabase.Refresh();

						// Change import settings.
						var textureImporter = (TextureImporter) TextureImporter.GetAtPath( textureAssetDatabaseFilePath );
						textureImporter.maxTextureSize = Mathf.Max( resolution.x, resolution.y );
						EditorUtility.SetDirty( textureImporter );
						textureImporter.SaveAndReimport();

						var textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>( textureAssetDatabaseFilePath );
						texturePropAndAssetLookup.Add( ( data.Key, textureAsset ) );
					}
					
					// Create and store mesh.
					var mesh = SubmeshMergerUtility.MergeMeshesAndSubmeshes( meshFilters, gridDimensions );
					string meshFileName = baseName + ".asset";
					string meshAssetDatabaseFilePath = assetDatabaseDirectoryPath + "/" + meshFileName;
					AssetDatabase.CreateAsset( mesh, meshAssetDatabaseFilePath );
					var meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>( meshAssetDatabaseFilePath );

					// Create material.
					Material mergedMaterial = new Material( shader );
					foreach( var texturePropAsse in texturePropAndAssetLookup ) mergedMaterial.SetTexture( texturePropAsse.Item1, texturePropAsse.Item2 );
					string materialFileName = baseName + ".mat";
					string materialAssetDatabaseFilePath = assetDatabaseDirectoryPath + "/" + materialFileName;
					AssetDatabase.CreateAsset( mergedMaterial, materialAssetDatabaseFilePath );
					var materialAsset = AssetDatabase.LoadAssetAtPath<Material>( materialAssetDatabaseFilePath ); 

					// Create prefab.
					GameObject prefab = new GameObject();
					var meshFilter = prefab.AddComponent<MeshFilter>();
					var meshRenderer = prefab.AddComponent<MeshRenderer>();
					meshFilter.sharedMesh = meshAsset;
					meshRenderer.sharedMaterial = materialAsset;
					prefab.name = baseName;
					string prefabFileName = baseName + ".prefab";
					string prefabAssetDatabaseFilePath = assetDatabaseDirectoryPath + "/" + prefabFileName;
					PrefabUtility.SaveAsPrefabAssetAndConnect( prefab, prefabAssetDatabaseFilePath, InteractionMode.UserAction );

					// Done.
					AssetDatabase.Refresh();
				}
			};
		}


		static void GenerateTextureGridPreviews( VisualElement container, int columnCount, int rowCount, Dictionary<string,List<Texture2D>> textureLookup )
		{
			container.Clear();

			foreach( var data in textureLookup )
			{
				var previewContainer = new VisualElement();
				previewContainer.style.width = new StyleLength( new Length( 100, LengthUnit.Percent ) );
				container.Add( previewContainer );
				container.Add( CreateVerticalSpacer() );
				GenerateTextureGridPreview( previewContainer, data.Key, columnCount, rowCount, data.Value );
			}
		}


		static void GenerateTextureGridPreview( VisualElement container, string texturePropertyName, int columnCount, int rowCount, List<Texture2D> textures )
		{
			container.Add( new Label(){ text = texturePropertyName } );

			float totalWidth = 200f;//container.resolvedStyle.width; // We have to wait for GeometryChanged to get the resolved with, so fuck that. https://discussions.unity.com/t/width-of-label/755474/4
			float tileSize = totalWidth / (float) columnCount;
			for( int r = 0; r < rowCount; r++ )
			{
				var rowContainer = new VisualElement();
				rowContainer.style.flexDirection = FlexDirection.Row;
				container.Add( rowContainer );
				for( int c = 0; c < columnCount; c++ )
				{
					int t = ( (rowCount-1) * columnCount ) - r * columnCount + c;
					var tileElement = new VisualElement();
					tileElement.style.width = tileSize;
					tileElement.style.height = tileSize;
					var tileLabel = new Label(){ text = t.ToString() };
					tileLabel.style.backgroundColor = Color.black;
					tileLabel.style.alignSelf = Align.FlexStart;
					tileElement.Add( tileLabel );
					if( t < textures.Count ){
						tileElement.style.backgroundImage = new StyleBackground( textures[ t ] );
					} else {
						tileElement.style.backgroundColor = Color.black;// Color.HSVToRGB( t / (float) ( rowCount * columnCount ), 0.5f, 1f );
					}
					rowContainer.Add( tileElement );
				}
			}
		}


		static VisualElement CreateVerticalSpacer()
		{
			var element = new VisualElement();
			element.style.height = verticalSpacing;
			return element;
		}


		[Serializable]
		public enum Resolution { _512, _1024, _2048, _4098, _8192, _16384 }
	}
}
