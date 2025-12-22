#!/bin/bash
set -e

FILTER=""
USE_RUST_BACKEND=false

while [[ $# -gt 0 ]]; do
  case $1 in
    --filter|-f)
      FILTER="$2"
      shift 2
      ;;
    --rust-backend)
      USE_RUST_BACKEND=true
      shift
      ;;
    *)
      FILTER="$1"
      shift
      ;;
  esac
done

# Toggle USE_RUST_BACKEND flag in csc.rsp
if [ "$USE_RUST_BACKEND" = true ]; then
  echo "Enabling Rust backend..."
  echo "-define:USE_RUST_BACKEND" > Assets/csc.rsp
  ./build-rust.sh
  echo ""
else
  echo "Using Burst backend (default)..."
  > Assets/csc.rsp
fi

FILTER_ARG=""
if [ -n "$FILTER" ]; then
  FILTER_ARG="-testFilter $FILTER"
  echo "Running Unity tests with filter: $FILTER"
else
  echo "Running Unity tests..."
fi

if [[ "$OSTYPE" == "darwin"* ]]; then
  UNITY_PATH="/Applications/Unity/Hub/Editor/6000.3.1f1/Unity.app/Contents/MacOS/Unity"
  PROJECT_PATH="/Users/dylanebert/KexEdit"
else
  UNITY_PATH="C:/Program Files/Unity/Hub/Editor/6000.3.1f1/Editor/Unity.exe"
  PROJECT_PATH="C:/Users/dylan/Documents/Games/KexEdit"
fi

"$UNITY_PATH" \
  -runTests -batchmode \
  -projectPath "$PROJECT_PATH" \
  -testResults "test-results.xml" \
  -logFile "test-log.txt" \
  -testPlatform EditMode \
  -assemblyNames "Tests" \
  $FILTER_ARG || true

if [ -f "test-results.xml" ]; then
  result=$(grep -o 'result="[^"]*"' test-results.xml | head -1 | sed 's/result="\(.*\)"/\1/')
  passed=$(grep -o 'passed="[^"]*"' test-results.xml | head -1 | sed 's/passed="\(.*\)"/\1/')
  failed=$(grep -o 'failed="[^"]*"' test-results.xml | head -1 | sed 's/failed="\(.*\)"/\1/')
  total=$(grep -o 'total="[^"]*"' test-results.xml | head -1 | sed 's/total="\(.*\)"/\1/')
  duration=$(grep -o 'duration="[^"]*"' test-results.xml | head -1 | sed 's/duration="\(.*\)"/\1/')

  echo ""
  echo "Tests: $result | Passed: $passed | Failed: $failed | Total: $total | Duration: ${duration}s"
  echo "Full logs: $PROJECT_PATH/test-log.txt"
  echo "Full results: $PROJECT_PATH/test-results.xml"

  if [ "$failed" != "0" ]; then
    echo ""
    echo "Failed tests:"
    grep 'result="Failed"' test-results.xml | grep -o 'fullname="[^"]*"' | sed 's/fullname="\(.*\)"/\1/'
    exit 1
  fi
fi
