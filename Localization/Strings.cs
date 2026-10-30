using System.Globalization;
using System.Text;

namespace EventvwrParser.Localization;

public static class Strings {
    public static bool IsEnglish { get; private set; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "es";

    public static void ToggleLanguage() => IsEnglish = !IsEnglish;

    static string Pick(string es, string en) => IsEnglish ? en : es;

    public static string FullDate(DateTime d) => d.ToString(Pick("dd/MM/yy HH:mm:ss", "yyyy-MM-dd HH:mm:ss"), CultureInfo.InvariantCulture);
    public static string ShortDate(DateTime d) => d.ToString(Pick("dd/MM HH:mm", "dd MMM HH:mm"), CultureInfo.InvariantCulture);

    public static string LanguageButton => Pick("EN", "ES");
    public static string LanguageTip => Pick("Cambiar a ingles", "Switch to Spanish");
    public static string LogonTip => Pick(
        "hora de logon (explorer.exe), lo que este despues de esto esta en instancia",
        "Logon time (explorer.exe start), anything after this is in instance");
    public static string SearchHint => Pick("Buscar... ej: comando ejecutado, iex, prefetch", "Search... e.g. command executed, iex, prefetch");
    public static string Refresh => Pick("Actualizar", "Refresh");
    public static string OnlyInstance => Pick("Solo en instancia", "In instance only");
    public static string HideDuplicates => Pick("Ocultar duplicados", "Hide duplicates");
    public static string Categories => Pick("CATEGORIAS", "CATEGORIES");
    public static string AllEvents => Pick("Todos", "All");
    public static string ColumnTime => Pick("HORA", "TIME");
    public static string ColumnType => Pick("TIPO", "TYPE");
    public static string ColumnSummary => Pick("RESUMEN", "SUMMARY");
    public static string ReadingEvents => Pick("Leyendo eventos...", "Reading events...");
    public static string LoadingHint => Pick("esto puede tardar un toque", "this may take a moment");
    public static string SelectEvent => Pick("Selecciona un evento", "Select an event");
    public static string Copy => Pick("Copiar", "Copy");
    public static string InInstance => Pick("en instancia", "in instance");
    public static string OutOfInstance => Pick("fuera de instancia", "out of instance");
    public static string ApproxTime => Pick("la hora es aprox (ultima vez q se modifico el archivo)", "approximate time (file's last modified date)");

    public static string Duplicates(int n) => Pick($"+{n} duplicados", n == 1 ? "+1 duplicate" : $"+{n} duplicates");
    public static string LoadedIn(double seconds) => Pick($"cargado en {seconds:0.0}s", $"loaded in {seconds:0.0}s");
    public static string Status(int total, int shown, int fileless) => Pick(
        $"{total} eventos | mostrando {shown} | {fileless} fileless",
        $"{total} events | showing {shown} | {fileless} fileless");

    public static readonly Localized GroupPowerShell = "PowerShell";
    public static readonly Localized GroupJournal = "Journal";
    public static readonly Localized GroupLogs = "Logs";
    public static readonly Localized GroupSystem = new("Sistema", "System");
    public static readonly Localized GroupTasks = new("Tareas programadas", "Scheduled tasks");

    public static readonly Localized TypeCommand = new("Comando ejecutado", "Command executed");
    public static readonly Localized TypeScript = new("Script ejecutado", "Script executed");
    public static readonly Localized TypePipeline = "Pipeline .NET / cmdlet";
    public static readonly Localized TypeCmdlet = "cmdlet";
    public static readonly Localized TypePsReadLine = new("Historial PSReadLine", "PSReadLine history");
    public static readonly Localized TypePowerShellProcess = new("Proceso PowerShell", "PowerShell process");
    public static readonly Localized TypeUsnDeleted = new("Journal USN borrado", "USN journal deleted");
    public static readonly Localized TypeJournalFsutil = new("Journal borrado (fsutil)", "Journal deleted (fsutil)");
    public static readonly Localized TypeJournalDeleted = new("Journal eliminado", "Journal deleted");
    public static readonly Localized TypeLogCleared = new("Log de eventos borrado", "Event log cleared");
    public static readonly Localized TypeServiceInstalled = new("Servicio instalado", "Service installed");
    public static readonly Localized TypeProcessCreated = new("Proceso creado", "Process created");
    public static readonly Localized TypeTaskStart = new("Inicio de tarea", "Task started");
    public static readonly Localized TypeTaskEnd = new("Finalizacion de tarea", "Task finished");
    public static readonly Localized TypeTaskChange = new("Tarea creada/modificada", "Task created/modified");

    public static readonly Localized TaskStarted = new("La tarea inicio su ejecucion", "Task started");
    public static readonly Localized TaskStartFailed = new("Error al iniciar la tarea", "Task failed to start");
    public static readonly Localized TaskTimeTrigger = new("Iniciada por trigger de tiempo", "Started by time trigger");
    public static readonly Localized TaskEventTrigger = new("Iniciada por trigger de evento", "Started by event trigger");
    public static readonly Localized TaskStartedByUser = new("Iniciada a mano por el usuario", "Started manually by the user");
    public static readonly Localized TaskStartedAtBoot = new("Iniciada al prender la pc", "Started at system startup");
    public static readonly Localized TaskStartedAtLogon = new("Iniciada al iniciar sesion", "Started at user logon");
    public static readonly Localized TaskActionCompleted = new("Se termino la accion de la tarea", "Task action completed");
    public static readonly Localized TaskInstanceFinished = new("Se finalizo la instancia de la tarea", "Task instance finished");
    public static readonly Localized TaskRegistered = new("Tarea registrada", "Task registered");
    public static readonly Localized TaskUpdated = new("Tarea actualizada o modificada", "Task updated or modified");
    public static readonly Localized TaskDeleted = new("Tarea eliminada", "Task deleted");
    public static readonly Localized TaskEvent = new("Evento de tarea", "Task event");

    public static readonly Localized SecurityLogCleared = new("Log de Seguridad borrado", "Security log cleared");

    public static readonly Localized ReasonAssemblyLoad = new("Carga Assembly.Load", "Uses Assembly.Load");
    public static readonly Localized ReasonMemoryApis = new("Usa APIs de memoria/inyeccion", "Uses memory/injection APIs");
    public static readonly Localized ReasonEncoded = new("Tiene base64 o -enc", "Contains base64 or -enc");
    public static readonly Localized ReasonStealth = new("Ventana oculta o bypass", "Hidden window or bypass");
    public static readonly Localized ReasonObfuscated = new("Script ofuscado", "Obfuscated script");
    public static readonly Localized ReasonFlaggedByPowerShell = new("PowerShell lo marco como sospechoso", "Flagged as suspicious by PowerShell");

    public static readonly Localized TaskHistoryDisabled = new("TaskScheduler: el historial esta desactivado", "TaskScheduler: history is disabled");

    public static Localized LogNotFound(string log) => new($"{log}: log no encontrado", $"{log}: log not found");
    public static Localized AccessDenied(string log) => new($"{log}: sin permisos, abrilo como admin", $"{log}: access denied, run it as admin");

    public static Localized ProcessDetails(string image, string cmd, string parent, string user) => new(
        $"Proceso: {image}\nLinea de comando: {cmd}\nProceso padre: {parent}\nUsuario: {user}",
        $"Process: {image}\nCommand line: {cmd}\nParent process: {parent}\nUser: {user}");

    public static Localized CmdletDetails(string payload, string context) => new(
        $"{payload}\n\nContexto: {context}",
        $"{payload}\n\nContext: {context}");

    public static Localized ScriptDetails(string id, string path, int parts, int total, string text) {
        string Build(string pathLabel, string partsLabel) {
            var header = new StringBuilder();
            header.AppendLine($"ScriptBlock ID: {id}");
            if (!string.IsNullOrWhiteSpace(path)) header.AppendLine($"{pathLabel}: {path}");
            if (total > 1) header.AppendLine($"{partsLabel}: {parts}/{total}");
            header.AppendLine();
            return header + text;
        }

        return new(Build("Ruta", "Partes"), Build("Path", "Parts"));
    }

    public static Localized PsReadLineDetails(string file, int number, int total, DateTime written, string cmd) => new(
        $"Archivo: {file}\nComando {number} de {total}\nUltima modificacion:{written:dd/MM/yyyy HH:mm:ss}\n\n{cmd}",
        $"File: {file}\nCommand {number} of {total}\nLast modified: {written.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}\n\n{cmd}");
}
