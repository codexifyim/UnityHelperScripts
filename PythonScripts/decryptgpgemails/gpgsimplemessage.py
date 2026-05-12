import gnupg
import os

gpg = gnupg.GPG()

def run_decryption():
    # Get Private Keys
    private_keys = gpg.list_keys(True)

    if not private_keys:
        print("No private keys found.")
        return

    print("--- Available Keys ---")
    for i, key in enumerate(private_keys):
        uid = key['uids'][0] if key['uids'] else "No UID"
        print(f"[{i}] {uid}")

    try:
        choice = int(input("\nSelect key number: "))
        selected_key = private_keys[choice]
    except (ValueError, IndexError):
        print("Invalid selection.")
        return

    # Automatically look for the file in the script's folder
    filename = input("Enter the filename (e.g., message.asc): ").strip()

    # Get the absolute path relative to this script
    base_path = os.path.dirname(os.path.abspath(__file__))
    file_path = os.path.join(base_path, filename)

    if not os.path.exists(file_path):
        print(f"Error: '{filename}' not found in {base_path}")
        return


    print(f"\nDecrypting {filename}...")
    print("(If a password window appeared behind your terminal, please check there!)")

    with open(file_path, "rb") as f:
        # Use gpg-agent to handle the passphrase prompt
        status = gpg.decrypt_file(f)

    if status.ok:
        print("\n--- Decrypted Message ---")
        print(str(status))
    else:
        print(f"\nDecryption failed: {status.status}")
        # This tells you IF it's a passphrase issue:
        if "Bad passphrase" in status.stderr or "Incomplete" in status.status:
            print("Hint: Incorrect passphrase or the password prompt was closed.")
        print(f"GPG Error Output: {status.stderr}")

if __name__ == "__main__":
    run_decryption()
