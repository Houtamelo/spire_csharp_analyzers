package spire.integration;

import com.intellij.openapi.application.ApplicationManager;
import com.intellij.openapi.editor.Editor;
import com.intellij.openapi.project.Project;
import com.intellij.openapi.startup.StartupActivity;
import com.intellij.psi.PsiManager;
import com.intellij.ui.popup.list.ListPopupImpl;
import com.jetbrains.rider.intentions.altEnter.RiderShowIntentionActionsHandler;
import com.jetbrains.rider.services.popups.nova.PopupItem;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Instant;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import javax.swing.event.ListDataEvent;
import javax.swing.event.ListDataListener;
import kotlin.Unit;
import org.jetbrains.annotations.NotNull;

public final class SpireIntegrationDriver implements StartupActivity.DumbAware {
  private static final String TITLE = "Replace with SpireEnum<T>.From(value)";
  private static final String CAST = "(ProbeKind)value";

  @Override
  public void runActivity(@NotNull Project project) {
    String report = System.getProperty("spire.integration.report");
    String root = System.getProperty("spire.integration.project");
    if (report == null || root == null || project.getBasePath() == null) return;
    if (!Path.of(root).toAbsolutePath().normalize().equals(Path.of(project.getBasePath()).toAbsolutePath().normalize())) return;
    ApplicationManager.getApplication().executeOnPooledThread(() -> run(project, Path.of(report)));
  }

  private void run(Project project, Path report) {
    Instant started = Instant.now();
    int attempts = 0;
    String inventory = "";
    try {
      write(report, "starting", started, attempts, false, false, "");
      Path source = Path.of(project.getBasePath(), "Assets/RuntimeAuto/_rider.cs");
      String before = Files.readString(source);
      String expected = before.replace(CAST, "SpireEnum<ProbeKind>.From(value)");
      int offset = before.indexOf(CAST);
      if (offset < 0) throw new IllegalStateException("The fixture has no target cast.");
      Thread.sleep(35000);
      AtomicReference<Editor> editorRef = new AtomicReference<>();
      ApplicationManager.getApplication().invokeAndWait(() -> {
        var file = com.intellij.openapi.vfs.LocalFileSystem.getInstance().refreshAndFindFileByNioFile(source);
        if (file == null) throw new IllegalStateException("The Rider fixture file is missing.");
        var manager = com.intellij.openapi.fileEditor.FileEditorManager.getInstance(project);
        for (var other : manager.getOpenFiles()) if (!other.equals(file)) manager.closeFile(other);
        Editor editor = manager.openTextEditor(new com.intellij.openapi.fileEditor.OpenFileDescriptor(project, file, offset), true);
        if (editor == null) throw new IllegalStateException("Rider did not open the fixture text editor.");
        manager.setSelectedEditor(file, "text-editor");
        editor.getCaretModel().moveToOffset(offset);
        com.intellij.openapi.wm.IdeFocusManager.getInstance(project).requestFocus(editor.getContentComponent(), true);
        editorRef.set(editor);
      });
      Thread.sleep(2000);
      write(report, "opened", started, attempts, false, false, "");
      long deadline = System.nanoTime() + TimeUnit.MINUTES.toNanos(10);
      boolean selected = false;
      while (!selected && System.nanoTime() < deadline && !project.isDisposed()) {
        attempts++;
        CompletableFuture<String> chosen = new CompletableFuture<>();
        AtomicReference<com.jetbrains.rider.intentions.altEnter.RiderPopupMenuController.PopupMenu> menuRef = new AtomicReference<>();
        StringBuilder offered = new StringBuilder();
        ApplicationManager.getApplication().invokeLater(() -> {
          try {
            Editor editor = editorRef.get();
            var file = com.intellij.openapi.vfs.LocalFileSystem.getInstance().findFileByNioFile(source);
            var psi = PsiManager.getInstance(project).findFile(file);
            menuRef.set(new RiderShowIntentionActionsHandler().createAndShowPopup(project, editor, psi, null, popup -> {
              ListPopupImpl list = (ListPopupImpl) popup;
              Runnable check = () -> {
                if (chosen.isDone()) return;
                var items = list.getList().getModel();
                for (int index = 0; index < items.getSize(); index++) {
                  Object value = items.getElementAt(index);
                  if (!(value instanceof PopupItem item)) continue;
                  String title = item.getDisplayName().getText();
                  offered.append(title).append(" | ");
                  if (title.startsWith(TITLE)) {
                    list.selectAndExecuteValue(value);
                    chosen.complete(offered.toString());
                    return;
                  }
                }
              };
              list.getList().getModel().addListDataListener(new ListDataListener() {
                public void intervalAdded(ListDataEvent event) { check.run(); }
                public void intervalRemoved(ListDataEvent event) { check.run(); }
                public void contentsChanged(ListDataEvent event) { check.run(); }
              });
              ApplicationManager.getApplication().invokeLater(check);
              return Unit.INSTANCE;
            }, false));
          } catch (Throwable error) { chosen.completeExceptionally(error); }
        });
        try {
          inventory = chosen.get(30, TimeUnit.SECONDS);
          selected = true;
          write(report, "discovered", started, attempts, true, false, inventory);
        } catch (java.util.concurrent.TimeoutException timeout) {
          inventory = offered.toString();
          write(report, "waiting", started, attempts, false, false, inventory);
        } finally {
          ApplicationManager.getApplication().invokeLater(() -> {
            var menu = menuRef.get();
            if (menu != null) menu.close();
          });
        }
        if (!selected) Thread.sleep(3000);
      }
      if (!selected) throw new IllegalStateException("Rider did not offer the expected fix: " + inventory);
      long saveDeadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(30);
      while (System.nanoTime() < saveDeadline) {
        ApplicationManager.getApplication().invokeAndWait(() -> com.intellij.openapi.fileEditor.FileDocumentManager.getInstance().saveAllDocuments());
        if (Files.readString(source).equals(expected)) break;
        Thread.sleep(200);
      }
      if (!Files.readString(source).equals(expected)) throw new IllegalStateException("Rider did not save the exact expected source transformation.");
      write(report, "pass", started, attempts, true, true, inventory);
    } catch (Throwable failure) {
      try { write(report, "fail", started, attempts, false, false, failure.toString()); }
      catch (Exception ignored) { }
    } finally {
      ApplicationManager.getApplication().invokeLater(() ->
          ((com.intellij.openapi.application.ex.ApplicationEx) ApplicationManager.getApplication()).exit(true, true, false));
    }
  }

  private void write(Path report, String status, Instant started, int attempts, boolean discovered, boolean applied, String detail) throws Exception {
    Files.writeString(report, "{\"status\":\"" + status + "\",\"started\":\"" + started +
        "\",\"finished\":\"" + Instant.now() + "\",\"attempts\":" + attempts +
        ",\"discovered\":" + discovered + ",\"applied\":" + applied +
        ",\"detail\":\"" + escape(detail) + "\"}\n");
  }

  private String escape(String value) {
    return value.replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t");
  }
}
