using StarkAgroAPI.Models.Entities;

namespace StarkAgroAPI.Tests.Models.Entities
{
    public class EntityNavigationTests
    {
        [Fact]
        public void Sensor_NavigationProperties_SetAndGet()
        {
            var sensor = new Sensor();
            var pivot = new Pivot { Id = 1, Name = "P1" };
            var user = new User { Id = 2, Name = "U1", Email = "u@e.com", Password = "p" };
            var reads = new List<ReadSensor> { new ReadSensor { Id = 1 } };

            sensor.Pivot = pivot;
            sensor.User = user;
            sensor.Reads = reads;

            Assert.Same(pivot, sensor.Pivot);
            Assert.Same(user, sensor.User);
            Assert.Same(reads, sensor.Reads);
        }

        [Fact]
        public void Pivot_Sensors_SetAndGet()
        {
            var pivot = new Pivot();
            var sensors = new List<Sensor> { new Sensor { Id = 1 } };

            pivot.Sensors = sensors;

            Assert.Same(sensors, pivot.Sensors);
        }

        [Fact]
        public void ReadSensor_Sensor_SetAndGet()
        {
            var readSensor = new ReadSensor();
            var sensor = new Sensor { Id = 1, Name = "S1" };

            readSensor.Sensor = sensor;

            Assert.Same(sensor, readSensor.Sensor);
        }
    }
}
