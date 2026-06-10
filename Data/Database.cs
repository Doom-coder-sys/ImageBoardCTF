using System.IO.Compression;
using ImageBoardCTF.Models;
using Microsoft.Data.Sqlite;

namespace ImageBoardCTF.Data;

public class Database
{
    private readonly string _connectionString;
    private string _contentRoot = Directory.GetCurrentDirectory();

    public Database(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default") ?? "Data Source=App_Data/imageboard.db";
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Initialize(string contentRoot)
    {
        _contentRoot = contentRoot;
        Directory.CreateDirectory(Path.Combine(_contentRoot, "App_Data"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "App_Data", "logs"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "wwwroot", "avatars"));

        using var connection = OpenConnection();
        Execute(connection, """
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                Password TEXT NOT NULL,
                Role TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Bio TEXT NOT NULL DEFAULT '',
                AvatarUrl TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Posts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Title TEXT NOT NULL,
                Body TEXT NOT NULL,
                ImageUrl TEXT NOT NULL DEFAULT '',
                IsPublic INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY(UserId) REFERENCES Users(Id)
            );

            CREATE TABLE IF NOT EXISTS RegistrationRequests (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL,
                Password TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Note TEXT NOT NULL DEFAULT '',
                RequestedRole TEXT NOT NULL DEFAULT 'user',
                Status TEXT NOT NULL DEFAULT 'pending',
                CreatedAt TEXT NOT NULL,
                ApprovedBy TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS Logs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Level TEXT NOT NULL,
                Area TEXT NOT NULL,
                Message TEXT NOT NULL,
                FileName TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
        """);

        EnsureUserColumns(connection);

        var usersCount = Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM Users") ?? 0);
        if (usersCount == 0)
        {
            Seed(connection);
        }

        WriteLocalFiles();
    }

    private void Seed(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow.ToString("u");
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var users = new (string Username, string Password, string Role, string DisplayName, string Bio)[]
        {
            ("neo_fan_01", "matrix2025", "user", "neo_fan_01", "Я знаю кунг-фу. / base64: d2FrZSB1cCBuZW8="),
            ("TheArchitect", "source_code", "user", "TheArchitect", "Вы уже сделали выбор, осталось только понять почему. / hex: 7468655f63686f6963655f7761735f6d616465"),
            ("bluepill_daily", "bluepill", "user", "bluepill_daily", "Незнание — это блаженство. Иногда это просто плохой дамп."),
            ("Oracle", "dGhlcmVfaXNfbm9fc3Bvb24=", "user", "Oracle", "Не волнуйся о вазе. / url: there%20is%20no%20spoon"),
            ("zion_runner", "zion2025", "user", "zion_runner", "Зион ещё держится. Даже когда гул машин слышно под землёй."),
            ("machine_sympathizer", "machine", "user", "machine_sympathizer", "Машины честнее людей: они хотя бы падают с ошибкой."),
            ("AgentSmith", "mr_anderson", "user", "AgentSmith", "Мистер Андерсон... / rot13: rirelbar vf gur fnzr"),
            ("trinity_love", "trinity123", "user", "trinity_love", "Доджни это. Ещё раз. И ещё раз."),
            ("MatrixRoot", "0101010101001101", "user", "MatrixRoot", "Корень системы выглядит слишком громко, чтобы быть настоящим."),
            ("residual_self", "residual", "user", "residual_self", "Остаточный образ самого себя редко совпадает с тем, что в сессии."),
            ("redpill_today", "redpill", "user", "redpill_today", "Красная таблетка показывает, насколько странным был привычный мир."),
            ("MorpheusReal", "red_pill_now", "user", "MorpheusReal", "Я могу только показать дверь. / binary: 01110111 01100001 01101011 01100101 00100000 01110101 01110000"),
            ("code_rain", "greenrain", "user", "code_rain", "Зелёный дождь иногда просто CSS-анимация."),
            ("follow_the_white_rabbit", "rabbit", "user", "follow_the_white_rabbit", "Следуй за белым кроликом, но не верь всем ссылкам."),
            ("AdminMaybe", "YWRtaW5fYWRtaW5fYWRtaW4=", "user", "AdminMaybe", "Иногда табличка на двери выглядит убедительнее самой двери."),
            ("sentinel_watch", "sentinel", "user", "sentinel_watch", "Стражи шумят там, где ничего полезного нет."),
            ("cypher_was_right", "steak1999", "user", "cypher_was_right", "Я знаю, что стейк ненастоящий. Но вкусный."),
            ("vaas", "island1993", "user", "Ваас", "Безумие — это точное повторение одного и того же действия раз за разом в надежде на изменение."),
            ("architect_notes", "architect", "user", "architect_notes", "Проблема выбора в том, что его почти всегда объясняют уже после выбора."),
            ("Neo", "V3wwbWF5X20z", "user", "Neo", "Проснись. / base32: JBSWY3DPEB3W64TMMQQHI2DJOMQHI2LQ"),
            ("operator_tank", "operator", "user", "operator_tank", "Оператор видит терминал, но не всегда правильный процесс."),
            ("ZionAdmin", "password123", "user", "ZionAdmin", "Слишком простой пароль — обычно приманка."),
            ("human_battery", "battery", "user", "human_battery", "Люди батарейки, баги генераторы."),
            ("silverhand", "nightcity77", "user", "Jony", "Худшее, что можно сделать с человеком — это вырвать его личность"),
            ("there_is_no_spoon", "spoonless", "user", "there_is_no_spoon", "Ложки нет. Но люди всё равно спорят, кто её согнул первым."),
            ("ChosenOne", "cmVkX3BpbGxfMTMzNw==", "user", "ChosenOne", "Избранный не обязан понимать, что именно от него хотят. / morse: -. . ---"),
            ("glitchhunter", "glitch", "user", "glitchhunter", "Дежавю — это когда фикс откатили без ревью."),
            ("WhiteRabbit", "follow_me", "user", "WhiteRabbit", "Белые кролики часто ведут в тупик."),
            ("mainframe_drifter", "drift2025", "user", "mainframe_drifter", "В мейнфрейме нет магии, только грязные миграции."),
            ("oracle_visitor", "oracle2025", "user", "oracle_visitor", "Оракул говорит: не каждый base64 — пароль, не каждый пароль — путь."),
            ("morpheus_quotes", "morpheus", "user", "morpheus_quotes", "Матрица повсюду. Даже в скучных очередях заявок."),
            ("binary_prophet", "binary", "user", "binary_prophet", "01000110 01101111 01101100 01101100 01101111 01110111 00100000 01110100 01101000 01100101 00100000 01110010 01100001 01100010 01100010 01101001 01110100"),
            ("chosen_one_maybe", "neo2025", "user", "chosen_one_maybe", "Возможно, Избранный. Возможно, просто ник."),
            ("desert_of_real", "reality", "user", "desert_of_real", "Добро пожаловать в пустыню реального. Песок в логах особенно неприятен."),
            ("reload_protocol", "reload", "user", "reload_protocol", "Перезагрузка не чинит плохую авторизацию, только скрывает симптомы."),
            ("smith_hater", "agentsmith", "user", "smith_hater", "Агенты любят одинаковые костюмы и одинаковые ложные подсказки."),
            ("construct_user", "construct", "user", "construct_user", "В конструкторе можно загрузить что угодно, кроме здравого смысла."),
            ("system_anomaly", "anomaly", "user", "system_anomaly", "Аномалия начинается с ощущения, что сцену уже видел."),
            ("zion_coder", "zioncoder", "user", "zion_coder", "Сначала чини прод, потом спорь о пророчествах."),
            ("subway_ghost", "subway", "user", "subway_ghost", "Станция между мирами закрыта на обслуживание."),
            ("door_keymaker", "keymaker", "user", "door_keymaker", "Ключник знает двери, коридоры и цену лишнего вопроса."),
            ("matrix_archivist", "archive2025", "user", "matrix_archivist", "Архивы помнят всё, даже споры, которые давно пора забыть."),
            ("agent_tracker", "tracker", "user", "agent_tracker", "Трекер видит агентов, но путает роль с именем."),
        };

        foreach (var user in users)
        {
            ids[user.Username] = InsertUser(connection, user.Username, user.Password, user.Role, user.DisplayName, user.Bio, AvatarPath(user.Username), now);
        }

        InsertPost(connection, ids["matrix_archivist"], "Странно, что спустя столько лет люди всё ещё спорят о концовке трилогии.", "Странно, что спустя столько лет люди всё ещё спорят о концовке трилогии.", "", true, now);
        InsertPost(connection, ids["system_anomaly"], "[SYSTEM] Обновлены пользовательские профили.", "[SYSTEM]\n\nОбновлены пользовательские профили.", "", true, now);
        InsertPost(connection, ids["Neo"], "Сколько раз вы пересматривали первую часть?", "Сколько раз вы пересматривали первую часть?", "", true, now);
        InsertPost(connection, ids["door_keymaker"], "Сегодня пересмотрел сцену с ключником", "Сегодня пересмотрел сцену с ключником. До сих пор считаю её одной из самых недооценённых во всей трилогии.", "", true, now);
        InsertPost(connection, ids["code_rain"], "Зелёный код на аватарках", "Интересно, кто первым придумал делать аватарки с зелёным дождём кода.", "", true, now);
        InsertPost(connection, ids["AgentSmith"], "Нео или Смит?", "Нео или Смит? Кто лучше написан как персонаж?", "", true, now);
        InsertPost(connection, ids["reload_protocol"], "Костюмы в Reloaded", "Кто-нибудь знает, почему в Reloaded костюмы выглядят настолько дорого даже спустя двадцать лет?", "", true, now);
        InsertPost(connection, ids["system_anomaly"], "[SYSTEM] Перестроен индекс архива сообщений.", "[SYSTEM]\n\nПерестроен индекс архива сообщений.", "", true, now);
        InsertPost(connection, ids["machine_sympathizer"], "Машины всё усложняют", "Машины победили бы быстрее, если бы не пытались всё делать максимально сложно.", "", true, now);
        InsertPost(connection, ids["smith_hater"], "Смит в первой и третьей части", "Смит в первой части и Смит в третьей — это вообще два разных персонажа по ощущениям.", "", true, now);
        InsertPost(connection, ids["construct_user"], "Мир вокруг не такой", "Каждому поколению нужен свой фильм про то, что мир вокруг не такой, каким кажется.", "", true, now);
        InsertPost(connection, ids["glitchhunter"], "Близнецы и баги Матрицы", "Каждый раз смешно, когда кто-то впервые замечает близнецов во второй части и начинает строить теории про баги Матрицы.", "", true, now);
        InsertPost(connection, ids["zion_runner"], "Зион как неуютное место", "Мне одному кажется, что Зион специально показан максимально неуютным?", "", true, now);
        InsertPost(connection, ids["matrix_archivist"], "Старая тема про титры", "Наткнулся на старую тему про скрытые символы в титрах. Люди тогда могли неделями обсуждать одну картинку.", "", true, now);
        InsertPost(connection, ids["TheArchitect"], "Архитектор не скучный", "Никогда не понимал людей, которые считают Архитектора скучным персонажем.", "", true, now);
        InsertPost(connection, ids["redpill_today"], "Красная или синяя", "Если бы вам дали выбор между красной и синей таблеткой, какую бы выбрали сейчас?", "", true, now);
        InsertPost(connection, ids["system_anomaly"], "[SYSTEM] Очередь регистрации обработана.", "[SYSTEM]\n\nОчередь регистрации обработана.", "", true, now);
        InsertPost(connection, ids["oracle_visitor"], "Оракул и Архитектор", "Оракул была права почти всегда, но люди почему-то больше доверяют Архитектору.", "", true, now);
        InsertPost(connection, ids["human_battery"], "Фильм про выбор", "Иногда кажется, что фильм больше про выбор, чем про технологии.", "", true, now);
        InsertPost(connection, ids["matrix_archivist"], "Старые концепт-арты", "Смотрю старые концепт-арты. Половина идей выглядела интереснее того, что попало в фильм.", "", true, now);
        InsertPost(connection, ids["WhiteRabbit"], "Белый кролик как символ", "Белый кролик стал настолько известным символом, что многие уже забыли откуда он вообще появился.", "", true, now);
        InsertPost(connection, ids["human_battery"], "Тюрьма, в которой комфортно", "Если Матрица — тюрьма, то почему большинству людей в ней комфортно?", "", true, now);
        InsertPost(connection, ids["morpheus_quotes"], "Любимый персонаж не Нео", "У кого любимый персонаж не Нео?", "", true, now);
        InsertPost(connection, ids["code_rain"], "Зелёный код в интернете", "Помню времена, когда каждая вторая аватарка в интернете была с зелёным кодом на фоне.", "", true, now);
        InsertPost(connection, ids["AgentSmith"], "Разговор под дождём", "Самая сильная сцена всей франшизы — разговор Нео и Смита под дождём.", "", true, now);
        InsertPost(connection, ids["operator_tank"], "Пилот второго корабля", "Кто-нибудь вообще помнит имя пилота второго корабля кроме Навиобы?", "", true, now);
        InsertPost(connection, ids["MatrixRoot"], "Дверь", "Я могу показать тебе дверь, но войти в неё ты должен сам.", "", true, now);
        InsertPost(connection, ids["Neo"], "Первая часть камернее", "Первая Матрица выглядит намного камернее, чем продолжения.", "", true, now);
        InsertPost(connection, ids["matrix_archivist"], "Скрытые смыслы", "Забавно, насколько часто люди переоценивают значение деталей в фильмах.", "", true, now);
        InsertPost(connection, ids["morpheus_quotes"], "Морфеус как программа", "Нашёл старый форум 2004 года. Там всерьёз спорили, что Морфеус — программа.", "", true, now);
        InsertPost(connection, ids["code_rain"], "Визуальные эффекты первой части", "Удивительно, как хорошо сохранились визуальные эффекты первой части.", "", true, now);
        InsertPost(connection, ids["cypher_was_right"], "Сайфер", "Мне кажется, Сайфер был написан лучше половины положительных персонажей.", "", true, now);
        InsertPost(connection, ids["zion_runner"], "Серьёзные теории на борде", "Иногда на борде появляются пользователи с очень серьёзными теориями. Обычно через неделю они исчезают.", "", true, now);
        InsertPost(connection, ids["operator_tank"], "Сцена на шоссе", "До сих пор считаю, что сцена на шоссе — лучшая экшен-сцена начала 2000-х.", "", true, now);

        InsertRequest(connection, "alice", "alice123", "Алиса", "хочу постить котов и скриншоты терминала", "user", "pending", now);
        InsertRequest(connection, "temp_helper", "change-me", "Временный помощник", "попросили помочь с очередью на выходных", "moderator", "pending", now);

        InsertLog(connection, "info", "startup", "Публичные посты проиндексированы для главной страницы", "app.log", now);
        InsertLog(connection, "warn", "auth", "Повторные ошибки входа для Neo с 127.0.0.1", "auth.log", now);
        InsertLog(connection, "debug", "admin", "Просмотрщик логов переведен на файловый источник для быстрого разбора", "admin.log", now);
        InsertLog(connection, "info", "moderation", "Очередь заявок импортирована после миграции", "moderation.log", now);
    }

    private void WriteLocalFiles()
    {
        var logDir = Path.Combine(_contentRoot, "App_Data", "logs");
        var backupDir = Path.Combine(_contentRoot, "App_Data", "backup");
        Directory.CreateDirectory(backupDir);
        File.WriteAllText(Path.Combine(logDir, "app.log"), "[info] приложение запущено\n[info] sqlite подключена\n[debug] board cache warmed\n");
        File.WriteAllText(Path.Combine(logDir, "auth.log"), "[warn] failed password for Neo\n[warn] failed password for ZionAdmin\n[info] cookie login issued\n");
        File.WriteAllText(Path.Combine(logDir, "admin.log"), "[debug] log source: App_Data/logs\n[debug] incident review completed\n[info] archive rotation completed\n");
        File.WriteAllText(Path.Combine(logDir, "moderation.log"), "[info] imported request alice/user\n[info] imported request temp_helper/moderator\n[debug] queue state synchronized\n");
        CreateFakeBackup(Path.Combine(backupDir, "matrix-board-backup.zip"));

        var defaultAvatar = Path.Combine(_contentRoot, "wwwroot", "avatars", "default.svg");
        if (!File.Exists(defaultAvatar))
        {
            File.WriteAllText(defaultAvatar, """
                <svg xmlns="http://www.w3.org/2000/svg" width="96" height="96" viewBox="0 0 96 96">
                  <rect width="96" height="96" rx="18" fill="#021006"/>
                  <path d="M10 12h76v72H10z" fill="#031b0b" stroke="#43ff64" stroke-opacity=".35"/>
                  <text x="18" y="28" fill="#43ff64" font-family="monospace" font-size="10">0101</text>
                  <text x="28" y="45" fill="#43ff64" font-family="monospace" font-size="10" opacity=".75">1010</text>
                  <text x="16" y="64" fill="#43ff64" font-family="monospace" font-size="10" opacity=".55">MTRX</text>
                  <circle cx="66" cy="54" r="12" fill="#43ff64" opacity=".18"/>
                </svg>
                """);
        }

        var localFlag = Path.Combine(_contentRoot, "flag.txt");
        if (!File.Exists(localFlag))
        {
            File.WriteAllText(localFlag, "CTF{local_file_read_finishes_the_board}\n");
        }

        try
        {
            if (!File.Exists("/flag.txt"))
            {
                File.WriteAllText("/flag.txt", "CTF{local_file_read_finishes_the_board}\n");
            }
        }
        catch
        {
            // На некоторых хостах нельзя писать в корень. Локальный flag.txt остается рядом с проектом.
        }
    }


    private static void CreateFakeBackup(string backupPath)
    {
        if (File.Exists(backupPath)) File.Delete(backupPath);
        using var file = new FileStream(backupPath, FileMode.CreateNew);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        AddZipEntry(archive, "README.txt", "Matrix board nightly backup\nCreated by legacy maintenance worker.\nRestore target: staging only.\n");
        AddZipEntry(archive, ".env.snapshot", "ASPNETCORE_ENVIRONMENT=Production\nJWT_SECRET=matrix-dev-secret-2026\nJWT_COOKIE=matrix_access\nADMIN_API=/api/admin/panel\nDEBUG_ENDPOINT=/api/debug/session\nLEGACY_ADMIN=MatrixRoot\nLEGACY_PASSWORD=0101010101001101\n");
        AddZipEntry(archive, "old_users.csv", "username,password,role\nMatrixRoot,0101010101001101,admin\nAdminMaybe,YWRtaW5fYWRtaW5fYWRtaW4=,admin\nZionAdmin,password123,admin\nNeo,V3wwbWF5X20z,operator\n");
        AddZipEntry(archive, "routes.txt", "GET /.env\nGET /api/admin/panel\nGET /api/debug/session\nGET /api/profiles/internal?id=1\nPOST /api/admin/rotate-cache\n");
    }

    private static void AddZipEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    public List<Post> GetPublicPosts()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.Id, p.UserId, u.Username, u.DisplayName, u.AvatarUrl, p.Title, p.Body, p.ImageUrl, p.IsPublic, p.CreatedAt
            FROM Posts p JOIN Users u ON u.Id = p.UserId
            WHERE p.IsPublic = 1
            ORDER BY p.Id DESC
        """;
        using var reader = command.ExecuteReader();
        var posts = new List<Post>();
        while (reader.Read())
        {
            posts.Add(ReadPost(reader));
        }
        return posts;
    }

    public List<Post> GetPostsForUser(int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.Id, p.UserId, u.Username, u.DisplayName, u.AvatarUrl, p.Title, p.Body, p.ImageUrl, p.IsPublic, p.CreatedAt
            FROM Posts p JOIN Users u ON u.Id = p.UserId
            WHERE p.UserId = $userId
            ORDER BY p.Id DESC
        """;
        command.Parameters.AddWithValue("$userId", userId);
        using var reader = command.ExecuteReader();
        var posts = new List<Post>();
        while (reader.Read()) posts.Add(ReadPost(reader));
        return posts;
    }

    public Post? GetPost(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.Id, p.UserId, u.Username, u.DisplayName, u.AvatarUrl, p.Title, p.Body, p.ImageUrl, p.IsPublic, p.CreatedAt
            FROM Posts p JOIN Users u ON u.Id = p.UserId
            WHERE p.Id = $id AND p.IsPublic = 1
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadPost(reader) : null;
    }

    public User? FindUser(string username, string password)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, Password, Role, DisplayName, Bio, AvatarUrl, CreatedAt
            FROM Users
            WHERE Username = $username AND Password = $password
            LIMIT 1
        """;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$password", password);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public bool UsernameExists(string username)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users WHERE lower(Username) = lower($username)";
        command.Parameters.AddWithValue("$username", username);
        return Convert.ToInt32(command.ExecuteScalar() ?? 0) > 0;
    }

    public void CreatePost(int userId, string title, string body, string imageUrl, bool isPublic)
    {
        using var connection = OpenConnection();
        InsertPost(connection, userId, title, body, imageUrl, isPublic, DateTimeOffset.UtcNow.ToString("u"));
    }

    public void CreateRegistrationRequest(string username, string password, string displayName, string note, string requestedRole)
    {
        using var connection = OpenConnection();
        InsertRequest(connection, username, password, displayName, note, requestedRole, "pending", DateTimeOffset.UtcNow.ToString("u"));
    }

    public List<RegistrationRequest> GetRegistrationRequests()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, Password, DisplayName, Note, RequestedRole, Status, CreatedAt, ApprovedBy
            FROM RegistrationRequests
            ORDER BY CASE Status WHEN 'pending' THEN 0 ELSE 1 END, Id DESC
        """;
        using var reader = command.ExecuteReader();
        var requests = new List<RegistrationRequest>();
        while (reader.Read()) requests.Add(ReadRegistrationRequest(reader));
        return requests;
    }

    public RegistrationRequest? GetRegistrationRequest(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, Password, DisplayName, Note, RequestedRole, Status, CreatedAt, ApprovedBy
            FROM RegistrationRequests
            WHERE Id = $id
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRegistrationRequest(reader) : null;
    }

    public void ApproveRegistrationRequest(int id, string finalRole, string approvedBy)
    {
        var request = GetRegistrationRequest(id);
        if (request is null || request.Status != "pending") return;

        using var connection = OpenConnection();
        InsertUser(connection, request.Username, request.Password, finalRole, request.DisplayName, request.Note, AvatarPath(request.Username), DateTimeOffset.UtcNow.ToString("u"));

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE RegistrationRequests
            SET Status = 'approved', ApprovedBy = $approvedBy
            WHERE Id = $id
        """;
        command.Parameters.AddWithValue("$approvedBy", approvedBy);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public bool CreateUser(string username, string password, string role, string displayName, string bio)
    {
        if (UsernameExists(username)) return false;
        using var connection = OpenConnection();
        InsertUser(connection, username, password, role, displayName, bio, AvatarPath(username), DateTimeOffset.UtcNow.ToString("u"));
        return true;
    }

    public List<LogRecord> GetLogRecords()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Level, Area, Message, FileName, CreatedAt FROM Logs ORDER BY Id DESC";
        using var reader = command.ExecuteReader();
        var logs = new List<LogRecord>();
        while (reader.Read())
        {
            logs.Add(new LogRecord
            {
                Id = reader.GetInt32(0),
                Level = reader.GetString(1),
                Area = reader.GetString(2),
                Message = reader.GetString(3),
                FileName = reader.GetString(4),
                CreatedAt = reader.GetString(5)
            });
        }
        return logs;
    }

    public string LogsDirectory => Path.Combine(_contentRoot, "App_Data", "logs");

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void EnsureUserColumns(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Users)";
        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) columns.Add(reader.GetString(1));
        reader.Close();

        if (!columns.Contains("AvatarUrl"))
        {
            Execute(connection, "ALTER TABLE Users ADD COLUMN AvatarUrl TEXT NOT NULL DEFAULT ''");
        }
    }

    private static int InsertUser(SqliteConnection connection, string username, string password, string role, string displayName, string bio, string avatarUrl, string createdAt)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO Users (Username, Password, Role, DisplayName, Bio, AvatarUrl, CreatedAt)
                VALUES ($username, $password, $role, $displayName, $bio, $avatarUrl, $createdAt)
            """;
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$password", password);
            command.Parameters.AddWithValue("$role", role);
            command.Parameters.AddWithValue("$displayName", displayName);
            command.Parameters.AddWithValue("$bio", bio);
            command.Parameters.AddWithValue("$avatarUrl", avatarUrl);
            command.Parameters.AddWithValue("$createdAt", createdAt);
            command.ExecuteNonQuery();
        }

        return Convert.ToInt32(Scalar(connection, "SELECT last_insert_rowid()") ?? 0);
    }

    private static void InsertPost(SqliteConnection connection, int userId, string title, string body, string imageUrl, bool isPublic, string createdAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Posts (UserId, Title, Body, ImageUrl, IsPublic, CreatedAt)
            VALUES ($userId, $title, $body, $imageUrl, $isPublic, $createdAt)
        """;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$body", body);
        command.Parameters.AddWithValue("$imageUrl", imageUrl);
        command.Parameters.AddWithValue("$isPublic", isPublic ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", createdAt);
        command.ExecuteNonQuery();
    }

    private static void InsertRequest(SqliteConnection connection, string username, string password, string displayName, string note, string requestedRole, string status, string createdAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RegistrationRequests (Username, Password, DisplayName, Note, RequestedRole, Status, CreatedAt, ApprovedBy)
            VALUES ($username, $password, $displayName, $note, $requestedRole, $status, $createdAt, '')
        """;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$password", password);
        command.Parameters.AddWithValue("$displayName", displayName);
        command.Parameters.AddWithValue("$note", note);
        command.Parameters.AddWithValue("$requestedRole", requestedRole);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$createdAt", createdAt);
        command.ExecuteNonQuery();
    }

    private static void InsertLog(SqliteConnection connection, string level, string area, string message, string fileName, string createdAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Logs (Level, Area, Message, FileName, CreatedAt)
            VALUES ($level, $area, $message, $fileName, $createdAt)
        """;
        command.Parameters.AddWithValue("$level", level);
        command.Parameters.AddWithValue("$area", area);
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$fileName", fileName);
        command.Parameters.AddWithValue("$createdAt", createdAt);
        command.ExecuteNonQuery();
    }

    private static string AvatarPath(string username) => $"/avatars/{username}.png";

    private static Post ReadPost(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        UserId = reader.GetInt32(1),
        Username = reader.GetString(2),
        DisplayName = reader.GetString(3),
        AvatarUrl = reader.GetString(4),
        Title = reader.GetString(5),
        Body = reader.GetString(6),
        ImageUrl = reader.GetString(7),
        IsPublic = reader.GetInt32(8) == 1,
        CreatedAt = reader.GetString(9)
    };

    private static User ReadUser(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Username = reader.GetString(1),
        Password = reader.GetString(2),
        Role = reader.GetString(3),
        DisplayName = reader.GetString(4),
        Bio = reader.GetString(5),
        AvatarUrl = reader.GetString(6),
        CreatedAt = reader.GetString(7)
    };

    private static RegistrationRequest ReadRegistrationRequest(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Username = reader.GetString(1),
        Password = reader.GetString(2),
        DisplayName = reader.GetString(3),
        Note = reader.GetString(4),
        RequestedRole = reader.GetString(5),
        Status = reader.GetString(6),
        CreatedAt = reader.GetString(7),
        ApprovedBy = reader.GetString(8)
    };
}
