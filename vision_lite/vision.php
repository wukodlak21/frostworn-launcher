<?php
/*

█▄█ ███ ███ ███ ███ █┼┼█ ┼┼ ███ ███ ███
███ ┼█┼ █▄▄ ┼█┼ █┼█ ██▄█ ┼┼ █▄█ █▄█ ┼█┼
┼█┼ ▄█▄ ▄▄█ ▄█▄ █▄█ █┼██ ┼┼ █┼█ █┼┼ ▄█▄

Copyrights @ cybermist2 2023-present
API built by cybermist2@gmail.com

*/

namespace vISION;

header('Content-Type: application/json; charset=UTF-8');

// CONFIGS
require('configs/config.php');
require('configs/slider.php');

// ENGINE
include('engine/downloads.php');

if ($_SERVER['REQUEST_METHOD'] === 'POST')
{
    $api = new APIHandle($config, $slider);
    $api->handle($_POST);
}
elseif ($_SERVER['REQUEST_METHOD'] === 'GET' && isset($_GET['execute']))
{
    $api = new APIHandle($config, $slider);
    $api->handle($_GET);
}
else
{
    http_response_code(400);
    echo json_encode(array('error' => 'Invalid execution request, this message is ok.'));
}

class APIHandle
{
    private $config;
    private $slider;

    public function __construct($config, $slider)
    {
        $this->config = $config;
        $this->slider = $slider;
    }

    public function handle($VAR)
    {
        $caseFunctions = [
            1   => 'handleSlides',
            2   => 'handleListGameFiles',
            3   => 'handleLauncherVersion',
            4   => 'handleServerStatus',
            5   => 'handleMostWanted',
        ];

        if (isset($caseFunctions[$VAR['execute']]))
        {
            $functionName = $caseFunctions[$VAR['execute']];
            $this->$functionName($VAR);
        }
        elseif ($VAR['execute'] == 999)
        {
            // Test purpose
        }
    }

    private function handleSlides()
    {
        echo json_encode($this->slider, JSON_PRETTY_PRINT);
    }

    private function handleListGameFiles()
    {
        $updater = new DOWNLOADS($this->config);
        echo $updater->ListGameFiles();
    }

    private function handleLauncherVersion()
    {
        // Must match the launcher's AssemblyVersion (Source/Properties/AssemblyInfo.cs).
        // Bump this together with AssemblyVersion/AssemblyFileVersion and set 'url' to the
        // new build's download link whenever a launcher self-update should be pushed.
        echo json_encode([
            'version' => '1.0.0.5',
            'url'     => 'https://frostworn.com/download/FrostwornLauncher.exe',
        ], JSON_PRETTY_PRINT);
    }

    // Live online player count per realm, for the launcher's Server Status panel.
    // Uses a WireGuard-only, column-scoped read-only DB account (see /var/www/.env,
    // LAUNCHER_STATUS_DB_*). TEST realm intentionally excluded (internal, not player-facing).
    // Degrades to 'available' => false instead of a 500 if the DB/tunnel is unreachable.
    private function handleServerStatus()
    {
        $env = $this->loadStatusEnv();
        $realms = [
            ['key' => 'ICC', 'label' => 'Legacy X5',       'db' => $env['LAUNCHER_STATUS_DB_ICC'] ?? null],
            ['key' => 'PRG', 'label' => 'Thunderstorm X1', 'db' => $env['LAUNCHER_STATUS_DB_PRG'] ?? null],
        ];

        $host = $env['LAUNCHER_STATUS_DB_HOST'] ?? null;
        $port = $env['LAUNCHER_STATUS_DB_PORT'] ?? '3306';
        $user = $env['LAUNCHER_STATUS_DB_USER'] ?? null;
        $pass = $env['LAUNCHER_STATUS_DB_PASS'] ?? null;
        $authDb = $env['LAUNCHER_STATUS_DB_AUTH'] ?? 'acore_auth';

        if (!$host || !$user || !$pass) {
            echo json_encode(['available' => false, 'realms' => [], 'total' => 0], JSON_PRETTY_PRINT);
            return;
        }

        $result = ['available' => true, 'realms' => [], 'total' => 0];

        try {
            $pdo = new \PDO(
                "mysql:host={$host};port={$port};charset=utf8mb4",
                $user,
                $pass,
                [\PDO::ATTR_ERRMODE => \PDO::ERRMODE_EXCEPTION, \PDO::ATTR_TIMEOUT => 3]
            );

            $botPattern = "(a.username LIKE 'RNDBOT%%' OR a.username LIKE 'PRGBOT%%' OR a.username LIKE 'TESTBOT%%')";
            $sql = "SELECT "
                 . "SUM(CASE WHEN NOT {$botPattern} THEN 1 ELSE 0 END) AS players, "
                 . "SUM(CASE WHEN {$botPattern} THEN 1 ELSE 0 END) AS bots "
                 . "FROM %s.characters c "
                 . "JOIN {$authDb}.account a ON a.id = c.account "
                 . "WHERE c.online = 1";

            foreach ($realms as $realm) {
                if (!$realm['db']) continue;
                $stmt = $pdo->query(sprintf($sql, $realm['db']));
                $row = $stmt->fetch(\PDO::FETCH_ASSOC);
                $online = (int) ($row['players'] ?? 0);
                $bots = (int) ($row['bots'] ?? 0);
                $result['realms'][] = [
                    'key'    => $realm['key'],
                    'label'  => $realm['label'],
                    'online' => $online,
                    'bots'   => $bots,
                ];
                $result['total'] += $online;
            }
        } catch (\Throwable $e) {
            $result = ['available' => false, 'realms' => [], 'total' => 0];
        }

        echo json_encode($result, JSON_PRETTY_PRINT);
    }

    // Top 3 "Most Wanted" players (Crime & Bounty System) for the given realm, for the
    // launcher's Most Wanted panel. Same read-only WireGuard-only DB account as handleServerStatus.
    // 'realm' must be ICC or PRG - anything else (or missing) degrades to 'available' => false.
    private function handleMostWanted($VAR)
    {
        $realm = isset($VAR['realm']) ? strtoupper($VAR['realm']) : '';
        $env = $this->loadStatusEnv();

        $worldDbKey = $realm === 'ICC' ? 'LAUNCHER_STATUS_DB_WORLD_ICC'
                    : ($realm === 'PRG' ? 'LAUNCHER_STATUS_DB_WORLD_PRG' : null);

        $host = $env['LAUNCHER_STATUS_DB_HOST'] ?? null;
        $port = $env['LAUNCHER_STATUS_DB_PORT'] ?? '3306';
        $user = $env['LAUNCHER_STATUS_DB_USER'] ?? null;
        $pass = $env['LAUNCHER_STATUS_DB_PASS'] ?? null;
        $worldDb = $worldDbKey ? ($env[$worldDbKey] ?? null) : null;

        if (!$host || !$user || !$pass || !$worldDb) {
            echo json_encode(['available' => false, 'realm' => $realm, 'players' => []], JSON_PRETTY_PRINT);
            return;
        }

        $result = ['available' => true, 'realm' => $realm, 'players' => []];

        try {
            $pdo = new \PDO(
                "mysql:host={$host};port={$port};charset=utf8mb4",
                $user,
                $pass,
                [\PDO::ATTR_ERRMODE => \PDO::ERRMODE_EXCEPTION, \PDO::ATTR_TIMEOUT => 3]
            );

            $stmt = $pdo->query(
                "SELECT player_name, wanted_level, bounty FROM {$worldDb}.crime_system_players "
              . "WHERE wanted_level > 0 "
              . "ORDER BY wanted_level DESC, bounty DESC "
              . "LIMIT 3"
            );

            foreach ($stmt->fetchAll(\PDO::FETCH_ASSOC) as $row) {
                $result['players'][] = [
                    'name'   => $row['player_name'],
                    'level'  => (int) $row['wanted_level'],
                    // crime_system_players.bounty is stored in gold*1000; convert to plain gold for display.
                    'bounty' => (int) ($row['bounty'] / 1000),
                ];
            }
        } catch (\Throwable $e) {
            $result = ['available' => false, 'realm' => $realm, 'players' => []];
        }

        echo json_encode($result, JSON_PRETTY_PRINT);
    }

    private function loadStatusEnv()
    {
        $path = __DIR__ . '/../../.env';
        $env = [];
        if (!file_exists($path)) return $env;

        foreach (file($path, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES) as $line) {
            $line = trim($line);
            if ($line === '' || strpos($line, '#') === 0) continue;
            if (strpos($line, '=') === false) continue;
            list($k, $v) = explode('=', $line, 2);
            $env[trim($k)] = trim($v);
        }
        return $env;
    }
}
