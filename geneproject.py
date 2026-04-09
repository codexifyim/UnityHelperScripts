import os

def create_unity_folders(game_name):
    # Define the main directory and subdirectories
    main_folder = game_name
    subfolders = [
        "Assets",
        "UserSettings",
        "ProjectSettings",
        "Packages",
        "Library",
        "Assets/Scripts",
        "Assets/Prefabs",
        "Assets/Materials",
        "Assets/Textures",
        "Assets/Animations",
        "Assets/Scenes",
        "Assets/Audio",
        "Assets/Resources",
        "Assets/Plugins",
        "Assets/Editor",
    ]

    # Create main folder
    os.makedirs(main_folder, exist_ok=True)

    # Create subfolders
    for folder in subfolders:
        path = os.path.join(main_folder, folder)
        os.makedirs(path, exist_ok=True)

    print(f"Unity project structure for '{game_name}' created successfully!")

if __name__ == "__main__":
    game_name = input("Enter the game name: ")
    create_unity_folders(game_name)
