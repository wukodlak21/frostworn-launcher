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
}
