# Performance Log Files

The SubtleByte projects write module performance measurements to dedicated log
files under the `BepInEx/config/VeinWares SubtleByte/` directory to keep the
server console clean.

| Project | Log file |
|---------|----------|
| Production mod (`VeinWares.SubtleByte`) | `performance.log` |
| Rewrite prototype (`VeinWares.SubtleByte.Rewrite`) | `rewrite-performance.log` |
| Performance template (`templates/SubtleByte.Template`) | `template-performance.log` |

All trackers record:

- The module or operation label.
- The duration in milliseconds.
- A timestamp so you can correlate spikes with gameplay events.

Trackers automatically rotate once a log file grows beyond roughly 5 MiB. The
previous contents move to a `.bak` file next to the active log so only the two
most recent batches are kept on disk. You can safely delete either file between
test runs—the next plugin startup will recreate a fresh log.
