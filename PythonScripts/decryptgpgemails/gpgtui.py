import gnupg
from textual.app import App, ComposeResult
from textual.widgets import Header, Footer, Static, ListItem, ListView, Label, TextArea
from textual.containers import Container, ScrollableContainer
from textual.binding import Binding


gpg = gnupg.GPG()

class GPGDecryptor(App):
    BINDINGS = [
        Binding("q", "quit", "Quit"),
        Binding("r", "reset_fields", "Reset"),
        Binding("ctrl+d", "decrypt_text", "Decrypt Paste", show=True),
    ]

    CSS = """
    Screen {
        align: center middle;
    }
    #main-container {
        width: 95%;
        height: 95%;
        border: solid green;
        padding: 1;
    }
    ListView {
        margin-bottom: 1;
        border: solid gray;
        height: 5;
    }
    #input-text {
        height: 10;
        border: solid yellow;
        margin-bottom: 1;
    }
    #scroll-section {
        background: $boost;
        margin-top: 1;
        border: tall white;
        height: 1fr;
    }
    #output {
        padding: 1;
    }
    Label {
        margin-top: 1;
        text-style: bold;
        color: cyan;
    }
    """

    def compose(self) -> ComposeResult:
        yield Header()
        self.keys = gpg.list_keys(True)

        with Container(id="main-container"):
            yield Label("1. Select Private Key:")
            with ListView(id="key-list"):
                for idx, key in enumerate(self.keys):
                    email = key['uids'][0] if key['uids'] else "Unknown"
                    yield ListItem(Label(f"[{idx}] {email}"), id=f"key_{idx}")

            yield Label("2. Paste Encrypted Message Here (Press Ctrl+D to Decrypt):")
            yield TextArea(id="input-text")

            yield Label("Decrypted Content:")
            with ScrollableContainer(id="scroll-section"):
                yield Static("Waiting for input...", id="output")

        yield Footer()

    def action_reset_fields(self) -> None:
        self.query_one("#input-text").text = ""
        self.query_one("#output").update("Ready for new message.")
        self.notify("Cleared")

    def action_decrypt_text(self) -> None:
        """Triggered by Ctrl+D"""
        if not hasattr(self, 'selected_key_index'):
            self.query_one("#output").update("[red]Error: Please select a KEY first![/red]")
            return

        text_to_decrypt = self.query_one("#input-text").text
        if not text_to_decrypt.strip():
            self.notify("Nothing to decrypt!", severity="error")
            return

        self.decrypt_message(text_to_decrypt)

    def on_list_view_selected(self, event: ListView.Selected):
        self.selected_key_index = event.list_view.index
        self.notify(f"Key selected: {self.keys[self.selected_key_index]['uids'][0]}")

    def decrypt_message(self, encrypted_string: str):
        output_widget = self.query_one("#output")
        try:
            # decrypt works with strings directly
            decrypted_data = gpg.decrypt(encrypted_string)

            if decrypted_data.ok:
                output_widget.update(str(decrypted_data))
                self.notify("Decryption Successful")
            else:
                output_widget.update(f"[red]Decryption Failed:[/red]\n{decrypted_data.status}\n{decrypted_data.stderr}")

        except Exception as e:
            output_widget.update(f"[red]Error: {str(e)}[/red]")

if __name__ == "__main__":
    app = GPGDecryptor()
    app.run()
