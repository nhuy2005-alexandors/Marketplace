function Replace-AtomicCacheFile([string]$SourcePath, [string]$DestinationPath) {
    $source = [IO.Path]::GetFullPath($SourcePath)
    $destination = [IO.Path]::GetFullPath($DestinationPath)

    if ([IO.File]::Exists($destination)) {
        [IO.File]::Replace($source, $destination, [NullString]::Value)
    }
    else {
        [IO.File]::Move($source, $destination)
    }
}

function Write-AtomicCacheFile([string]$Path, [string]$Content) {
    $destination = [IO.Path]::GetFullPath($Path)
    $directory = [IO.Path]::GetDirectoryName($destination)
    [void][IO.Directory]::CreateDirectory($directory)
    $temporary = Join-Path $directory ('.{0}.{1}.tmp' -f [IO.Path]::GetFileName($destination), [Guid]::NewGuid().ToString('N'))

    try {
        [IO.File]::WriteAllText($temporary, $Content, [Text.UTF8Encoding]::new($false))
        Replace-AtomicCacheFile -SourcePath $temporary -DestinationPath $destination
    }
    finally {
        if ([IO.File]::Exists($temporary)) {
            [IO.File]::Delete($temporary)
        }
    }
}
