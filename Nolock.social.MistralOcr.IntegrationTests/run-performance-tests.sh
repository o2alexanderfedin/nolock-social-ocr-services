#!/bin/bash

# MistralOcr Performance Test Runner
# This script provides convenient commands to run different types of performance tests

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if MISTRAL_API_KEY is set
check_api_key() {
    if [[ -z "${MISTRAL_API_KEY}" ]]; then
        print_error "MISTRAL_API_KEY environment variable is not set!"
        print_error "Please set it with: export MISTRAL_API_KEY=\"your-api-key\""
        exit 1
    fi
    print_success "MISTRAL_API_KEY is configured"
}

# Function to run specific test categories
run_tests() {
    local filter="$1"
    local description="$2"
    
    print_status "Running $description..."
    
    dotnet test --filter "$filter" \
        --logger "console;verbosity=normal" \
        --logger "trx;LogFileName=performance-results-$(date +%Y%m%d-%H%M%S).trx" \
        --collect:"XPlat Code Coverage" \
        --results-directory "./TestResults/"
    
    if [[ $? -eq 0 ]]; then
        print_success "$description completed successfully"
    else
        print_error "$description failed"
        exit 1
    fi
}

# Function to display help
show_help() {
    echo "MistralOcr Performance Test Runner"
    echo ""
    echo "Usage: $0 [COMMAND]"
    echo ""
    echo "Commands:"
    echo "  all                 Run all performance tests (except long-running ones)"
    echo "  response-time       Run response time benchmark tests"
    echo "  memory              Run memory usage validation tests"
    echo "  throughput          Run throughput testing"
    echo "  connection-pooling  Run connection pooling efficiency tests"
    echo "  stream-processing   Run stream processing performance tests"
    echo "  advanced            Run advanced performance tests"
    echo "  load-test           Run load tests (long-running, skipped by default)"
    echo "  regression          Run performance regression tests"
    echo "  quick               Run a quick subset of performance tests"
    echo "  help                Show this help message"
    echo ""
    echo "Environment Variables:"
    echo "  MISTRAL_API_KEY     Required API key for Mistral service"
    echo ""
    echo "Examples:"
    echo "  export MISTRAL_API_KEY=\"your-key\""
    echo "  $0 all"
    echo "  $0 response-time"
    echo "  $0 quick"
}

# Main script logic
main() {
    local command="${1:-help}"
    
    print_status "MistralOcr Performance Test Runner"
    print_status "Command: $command"
    
    case "$command" in
        "all")
            check_api_key
            run_tests "FullyQualifiedName~PerformanceTests&Category!=LongRunning" "All Performance Tests"
            ;;
        "response-time")
            check_api_key
            run_tests "FullyQualifiedName~ResponseTime" "Response Time Benchmarks"
            ;;
        "memory")
            check_api_key
            run_tests "FullyQualifiedName~MemoryUsage" "Memory Usage Validation"
            ;;
        "throughput")
            check_api_key
            run_tests "FullyQualifiedName~Throughput" "Throughput Testing"
            ;;
        "connection-pooling")
            check_api_key
            run_tests "FullyQualifiedName~ConnectionPooling" "Connection Pooling Efficiency"
            ;;
        "stream-processing")
            check_api_key
            run_tests "FullyQualifiedName~StreamProcessing" "Stream Processing Performance"
            ;;
        "advanced")
            check_api_key
            run_tests "FullyQualifiedName~AdvancedPerformanceTests" "Advanced Performance Tests"
            ;;
        "load-test")
            check_api_key
            print_warning "Load tests are long-running and may consume API quota"
            read -p "Are you sure you want to run load tests? (y/N): " -n 1 -r
            echo
            if [[ $REPLY =~ ^[Yy]$ ]]; then
                # Note: This requires manually removing Skip attributes from load tests
                run_tests "FullyQualifiedName~LoadTest" "Load Tests"
            else
                print_status "Load tests cancelled"
            fi
            ;;
        "regression")
            check_api_key
            run_tests "FullyQualifiedName~PerformanceRegression" "Performance Regression Tests"
            ;;
        "quick")
            check_api_key
            print_status "Running quick performance test subset..."
            run_tests "FullyQualifiedName~(ResponseTime_SingleImageProcessing|MemoryUsage_SingleImageProcessing|Throughput_SequentialProcessing)" "Quick Performance Tests"
            ;;
        "help"|"-h"|"--help")
            show_help
            ;;
        *)
            print_error "Unknown command: $command"
            echo ""
            show_help
            exit 1
            ;;
    esac
    
    if [[ "$command" != "help" && "$command" != "-h" && "$command" != "--help" ]]; then
        print_success "Performance testing completed!"
        print_status "Test results saved in ./TestResults/ directory"
    fi
}

# Run main function with all arguments
main "$@"