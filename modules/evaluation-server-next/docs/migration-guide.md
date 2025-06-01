# FeatBit Evaluation Server Migration Guide
## From EvaluationServer.sln to FeatBit.EvaluationServer.sln

### Architecture Overview

The new evaluation server implementation has been restructured into a more modular and maintainable architecture, with clear separation of concerns. Here's how the key functionality has been migrated:

#### 1. Core Components

**Old Implementation**:
- Single monolithic evaluation server
- Tightly coupled message handling and evaluation logic
- Direct WebSocket connection handling

**New Implementation**:
- Split into three main components:
  - `FeatBit.EvaluationServer.Edge`: Handles client connections and initial request processing
  - `FeatBit.EvaluationServer.Hub`: Core evaluation and state management
  - `FeatBit.EvaluationServer.Broker`: Message broker integration

#### 2. Feature Flag Evaluation

**Old Implementation** (`Domain.Evaluation.Evaluator`):
- Monolithic evaluation in a single class
- Direct access to flag data
- Tightly coupled rule matching

**New Implementation** (`FeatBit.EvaluationServer.Hub.Evaluation`):
- Modular evaluation system with specialized components:
  - `IFlagEvaluator`: Main evaluation interface
  - `ITargetEvaluator`: User targeting evaluation
  - `IRuleEvaluator`: Rule evaluation
  - `IDistributionEvaluator`: Percentage rollout handling

#### 3. Message Broker Integration

**Old Implementation**:
- Basic message producer/consumer pattern
- Limited broker options
- Tightly coupled with the main server

**New Implementation** (`FeatBit.EvaluationServer.Broker`):
- Dedicated broker service
- Support for multiple broker types:
  - Redis
  - Kafka
  - PostgreSQL
- Improved message handling with:
  - Better error handling
  - Retry mechanisms
  - Health checks
  - Metrics collection

#### 4. State Management

**Old Implementation**:
- Direct database access for flag states
- In-memory caching with limited capabilities

**New Implementation** (`FeatBit.EvaluationServer.Hub.State`):
- Dedicated state management system
- `IStateManager` interface with implementations:
  - `InMemoryStateManager`: Fast local state
  - Support for distributed state (Redis, etc.)
- Efficient flag and target state tracking

#### 5. Configuration

**Old Implementation**:
- Basic configuration in appsettings.json
- Limited flexibility in broker selection

**New Implementation**:
- Enhanced configuration system
- Dynamic broker selection:
```json
{
  "Broker": {
    "Type": "redis|kafka|postgres",
    "Redis": {
      // Redis-specific config
    },
    "Kafka": {
      // Kafka-specific config
    },
    "Postgres": {
      // Postgres-specific config
    }
  }
}
```

### Key Improvements

1. **Modularity**: Clear separation of concerns between Edge, Hub, and Broker components
2. **Scalability**: Better support for horizontal scaling
3. **Maintainability**: More organized codebase with well-defined interfaces
4. **Performance**: Optimized state management and evaluation
5. **Reliability**: Improved error handling and health monitoring
6. **Flexibility**: Easy to extend with new broker implementations

### Migration Notes

1. **Message Handling**:
   - Update message producer/consumer implementations to use new interfaces
   - Migrate to new broker-specific configuration format
   - Update message payload formats to match new schema

2. **Feature Flag Evaluation**:
   - Move evaluation logic to use new Hub services
   - Update rule evaluation to use new modular evaluators
   - Migrate targeting rules to new format

3. **State Management**:
   - Move from direct database access to StateManager interface
   - Update caching strategy to use new state management system
   - Migrate existing state data to new format

4. **Configuration**:
   - Update appsettings.json to new format
   - Review and update broker-specific settings
   - Configure health checks and metrics

### Breaking Changes

1. Message broker interfaces have changed:
   - Old: `IMessageConsumer.Topic` property
   - New: `IMessageHandler.Topic` property

2. Evaluation interfaces have been restructured:
   - Old: Single `Evaluator` class
   - New: Multiple specialized evaluator interfaces

3. State management has been centralized:
   - Old: Direct data access
   - New: Through `IStateManager` 