<?php
/*

█▄█ ███ ███ ███ ███ █┼┼█ ┼┼ ███ ███ ███
███ ┼█┼ █▄▄ ┼█┼ █┼█ ██▄█ ┼┼ █▄█ █▄█ ┼█┼
┼█┼ ▄█▄ ▄▄█ ▄█▄ █▄█ █┼██ ┼┼ █┼█ █┼┼ ▄█▄

Copyrights @ cybermist2 2023-present
API built by cybermist2@gmail.com

*/

namespace vISION;

class DOWNLOADS
{
    protected $config;

    /**
     * Constructor for initializing the object with configuration and database parameters.
     *
     * @param mixed $config   The configuration data.
     */
    public function __construct($config)
    {
        $this->config = $config;
    }

    /*
    * Returns game files list as json
    *
    * @return json
    */
    public function ListGameFiles()
    {
        $GameFiles = [];

        $files = $this->GetGameFiles();

        $protocol = isset($_SERVER['HTTPS']) && $_SERVER['HTTPS'] === 'on' ? 'https://' : 'http://';
        $domain = $protocol . $_SERVER['HTTP_HOST'];

        foreach ($files as $file)
        {
            $fileInfo = new \stdClass();
            $fileInfo->Name = basename($file); // Get the file name
            $fileInfo->Size = $this->GetRealFileSize($file); // Get the file size
            $fileInfo->Timestamp = filemtime($file); // Get the file last modified time
            $fileInfo->IsHD = str_contains($file, "hd\\");

            $targetPath = str_replace('../'.$this->config['api_folder_name'].'/downloads/game', '', $file); // removes "../api folder name" from the path
            $targetPath = str_replace('sd\\', '', $targetPath); // removes "sd\" from the path
            $targetPath = str_replace('hd\\', '', $targetPath); // removes "sd\" from the path
            // or
            $targetPath = str_replace('sd/', '', $targetPath); // removes "sd/" from the path
            $targetPath = str_replace('hd/', '', $targetPath); // removes "sd/" from the path

            $fileInfo->TargetPath = $targetPath; // Get the file target path

            $file = str_replace('\\', '/', $file); // corrects with the valid backslashes
            $url = str_replace(['../', '..\\'], '', $domain . '/' . $file); // removes the "../" from the url path
            $fileInfo->Url = $url; // Get the file full url

            // Add the file info to the array
            $GameFiles[] = $fileInfo;
        }

        return json_encode($GameFiles, JSON_PRETTY_PRINT);
    }

    /*
    * Returns game files array
    *
    * @return array
    */
    private function GetGameFiles() : array
    {
        $iterator = new \RecursiveIteratorIterator
        (
            new \RecursiveDirectoryIterator("../".$this->config['api_folder_name']."/downloads/game", \RecursiveDirectoryIterator::SKIP_DOTS),
            \RecursiveIteratorIterator::SELF_FIRST
        );

        $files = [];

        foreach ($iterator as $file)
        {
            if ($file->isFile())
            {
                $files[] = $file->getPathname();
            }
        }

        return $files;
    }

    /*
    * Returns real file size, handling also bigger file size
    *
    * @param string $pfn - represents parent folder name
    * @return big or small integger
    */
    private function GetRealFileSize(string $path)
    {
        if (!file_exists($path))
            return false;

        $size = filesize($path);

        if (!($file = fopen($path, 'rb')))
            return false;

        if ($size >= 0)
        {
            // Check if it really is a small file (< 2 GB)
            if (fseek($file, 0, SEEK_END) === 0)
            {
                // It really is a small file
                fclose($file);
                return $size;
            }
        }

        // Quickly jump the first 2 GB with fseek. After that fseek is not working on 32 bit php (it uses int internally)
        $size = PHP_INT_MAX - 1;
        if (fseek($file, PHP_INT_MAX - 1) !== 0)
        {
            fclose($file);
            return false;
        }

        $length = 1024 * 1024;
        while (!feof($file))
        {
            // Read the file until end
            $read = fread($file, $length);
            $size = bcadd($size, $length);
        }

        $size = bcsub($size, $length);
        $size = bcadd($size, strlen($read));

        fclose($file);
        return $size;
    }
}