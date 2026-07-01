using System.Linq;
using System.Net;
using Abm.Pyro.Application.FhirPatch;
using Abm.Pyro.Domain.Exceptions;
using Hl7.Fhir.Model;
using Xunit;

namespace Abm.Pyro.Application.Test.FhirPatch;

/// <summary>
/// Unit tests for FhirPathPatchService.Apply().
/// FhirPathPatchService has no constructor dependencies, so these are pure unit tests
/// that construct the service directly and exercise the Firely ElementNode tree mutations.
///
/// Test resource: Patient with two HumanName entries, a telecom, active flag, and gender.
///   name[0]: family = "Duck",  given = ["Donald", "Fauntleroy"]
///   name[1]: family = "Mouse", given = ["Mickey"]
/// </summary>
public class FhirPathPatchServiceTest
{
    // ── test resource ────────────────────────────────────────────────────────

    private static Patient GetPatient() =>
        new()
        {
            Id     = "test-patient",
            Active = true,
            Gender = AdministrativeGender.Male,
            Name   =
            [
                new HumanName { Family = "Duck",  Given = ["Donald", "Fauntleroy"] },
                new HumanName { Family = "Mouse", Given = ["Mickey"] }
            ],
            Telecom =
            [
                new ContactPoint
                {
                    System = ContactPoint.ContactPointSystem.Phone,
                    Value  = "555-1234",
                    Use    = ContactPoint.ContactPointUse.Home
                }
            ]
        };

    // ── patch builder ────────────────────────────────────────────────────────
    // Avoids new List<>() because `using Hl7.Fhir.Model` shadows List<T> with the FHIR List resource.

    private static Parameters MakeOp(
        string    type,
        string    path,
        string?   name        = null,
        DataType? value       = null,
        int?      index       = null,
        int?      source      = null,
        int?      destination = null)
    {
        var op = new Parameters.ParameterComponent
        {
            Name = "operation",
            Part =
            [
                new() { Name = "type", Value = new Code(type) },
                new() { Name = "path", Value = new FhirString(path) }
            ]
        };
        if (name        is not null) op.Part.Add(new() { Name = "name",        Value = new FhirString(name) });
        if (value       is not null) op.Part.Add(new() { Name = "value",       Value = value });
        if (index       is not null) op.Part.Add(new() { Name = "index",       Value = new Integer(index) });
        if (source      is not null) op.Part.Add(new() { Name = "source",      Value = new Integer(source) });
        if (destination is not null) op.Part.Add(new() { Name = "destination", Value = new Integer(destination) });

        var p = new Parameters();
        p.Parameter.Add(op);
        return p;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Tests
    // ═══════════════════════════════════════════════════════════════════════

    public class Apply : FhirPathPatchServiceTest
    {
        private readonly FhirPathPatchService _sut = new();

        // ── replace ─────────────────────────────────────────────────────────

        [Fact]
        public void replace_boolean_primitive_changes_value()
        {
            Patient patient = GetPatient(); // active = true
            Parameters patch = MakeOp("replace", "Patient.active", value: new FhirBoolean(false));

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.False(patched.Active);
        }

        [Fact]
        public void replace_code_primitive_changes_gender()
        {
            Patient patient = GetPatient(); // gender = male
            Parameters patch = MakeOp("replace", "Patient.gender", value: new Code("female"));

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal(AdministrativeGender.Female, patched.Gender);
        }

        [Fact]
        public void replace_string_nested_changes_family_name()
        {
            Patient patient = GetPatient(); // name[0].family = "Duck"
            Parameters patch = MakeOp("replace", "Patient.name[0].family", value: new FhirString("Smith"));

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal("Smith", patched.Name[0].Family);
            Assert.Equal("Mouse", patched.Name[1].Family); // name[1] untouched
        }

        [Fact]
        public void replace_complex_element_swaps_whole_human_name()
        {
            Patient patient = GetPatient(); // name[0] = Duck / Donald Fauntleroy
            var newName = new HumanName { Family = "Pluto", Given = ["Rex"] };
            Parameters patch = MakeOp("replace", "Patient.name[0]", value: newName);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal("Pluto", patched.Name[0].Family);
            Assert.Equal("Rex",   patched.Name[0].Given.First());
            Assert.Equal("Mouse", patched.Name[1].Family); // name[1] untouched
            Assert.Equal(2, patched.Name.Count);
        }

        [Fact]
        public void replace_given_name_at_index_changes_single_entry()
        {
            Patient patient = GetPatient(); // name[0].given = ["Donald","Fauntleroy"]
            Parameters patch = MakeOp("replace", "Patient.name[0].given[1]", value: new FhirString("Percival"));

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            var given = patched.Name[0].Given.ToList();
            Assert.Equal("Donald",   given[0]);
            Assert.Equal("Percival", given[1]);
        }

        [Fact]
        public void replace_preserves_id_and_all_other_properties()
        {
            Patient patient = GetPatient();
            Parameters patch = MakeOp("replace", "Patient.active", value: new FhirBoolean(false));

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal("test-patient", patched.Id);
            Assert.Equal(AdministrativeGender.Male, patched.Gender);
            Assert.Equal(2, patched.Name.Count);
        }

        // ── add ─────────────────────────────────────────────────────────────

        [Fact]
        public void add_name_to_patient_with_no_names()
        {
            var patient = new Patient { Id = "p1" }; // no name at all
            var newName = new HumanName { Family = "Doe", Given = ["Jane"] };
            Parameters patch = MakeOp("add", "Patient", name: "name", value: newName);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Single(patched.Name);
            Assert.Equal("Doe",  patched.Name[0].Family);
            Assert.Equal("Jane", patched.Name[0].Given.First());
        }

        [Fact]
        public void add_name_to_patient_with_existing_names_appends()
        {
            Patient patient = GetPatient(); // already has name[0] Duck, name[1] Mouse
            var newName = new HumanName { Family = "Goofy" };
            Parameters patch = MakeOp("add", "Patient", name: "name", value: newName);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal(3, patched.Name.Count);
            Assert.Equal("Duck",  patched.Name[0].Family);
            Assert.Equal("Mouse", patched.Name[1].Family);
            Assert.Equal("Goofy", patched.Name[2].Family);
        }

        [Fact]
        public void add_given_name_to_existing_human_name()
        {
            Patient patient = GetPatient(); // name[1] Mouse / Mickey
            Parameters patch = MakeOp("add", "Patient.name[1]", name: "given", value: new FhirString("Minnie"));

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            var given = patched.Name[1].Given.ToList();
            Assert.Equal(2,        given.Count);
            Assert.Equal("Mickey", given[0]);
            Assert.Equal("Minnie", given[1]);
        }

        // ── insert ──────────────────────────────────────────────────────────

        [Fact]
        public void insert_name_at_index_zero_prepends()
        {
            Patient patient = GetPatient(); // [Duck, Mouse]
            var newName = new HumanName { Family = "New" };
            Parameters patch = MakeOp("insert", "Patient.name", value: newName, index: 0);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal(3, patched.Name.Count);
            Assert.Equal("New",   patched.Name[0].Family);
            Assert.Equal("Duck",  patched.Name[1].Family);
            Assert.Equal("Mouse", patched.Name[2].Family);
        }

        [Fact]
        public void insert_name_at_last_index_appends()
        {
            Patient patient = GetPatient(); // [Duck, Mouse] → count 2, valid insert idx = 0..2
            var newName = new HumanName { Family = "Last" };
            Parameters patch = MakeOp("insert", "Patient.name", value: newName, index: 2);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal(3, patched.Name.Count);
            Assert.Equal("Duck",  patched.Name[0].Family);
            Assert.Equal("Mouse", patched.Name[1].Family);
            Assert.Equal("Last",  patched.Name[2].Family);
        }

        [Fact]
        public void insert_name_in_middle_preserves_surrounding_order()
        {
            Patient patient = GetPatient(); // [Duck, Mouse]
            var newName = new HumanName { Family = "Middle" };
            Parameters patch = MakeOp("insert", "Patient.name", value: newName, index: 1);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal(3, patched.Name.Count);
            Assert.Equal("Duck",   patched.Name[0].Family);
            Assert.Equal("Middle", patched.Name[1].Family);
            Assert.Equal("Mouse",  patched.Name[2].Family);
        }

        [Fact]
        public void insert_given_at_index_zero_in_nested_array()
        {
            Patient patient = GetPatient(); // name[0].given = ["Donald","Fauntleroy"]
            Parameters patch = MakeOp("insert", "Patient.name[0].given", value: new FhirString("First"), index: 0);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            var given = patched.Name[0].Given.ToList();
            Assert.Equal("First",      given[0]);
            Assert.Equal("Donald",     given[1]);
            Assert.Equal("Fauntleroy", given[2]);
        }

        // ── delete ──────────────────────────────────────────────────────────

        [Fact]
        public void delete_scalar_property_removes_it()
        {
            Patient patient = GetPatient(); // active = true
            Parameters patch = MakeOp("delete", "Patient.active");

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Null(patched.Active);
        }

        [Fact]
        public void delete_first_array_item_shifts_remaining_items()
        {
            Patient patient = GetPatient(); // [Duck, Mouse]
            Parameters patch = MakeOp("delete", "Patient.name[0]");

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Single(patched.Name);
            Assert.Equal("Mouse", patched.Name[0].Family);
        }

        [Fact]
        public void delete_last_array_item_leaves_first_untouched()
        {
            Patient patient = GetPatient(); // [Duck, Mouse]
            Parameters patch = MakeOp("delete", "Patient.name[1]");

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Single(patched.Name);
            Assert.Equal("Duck", patched.Name[0].Family);
        }

        [Fact]
        public void delete_nested_given_name_at_index()
        {
            Patient patient = GetPatient(); // name[0].given = ["Donald","Fauntleroy"]
            Parameters patch = MakeOp("delete", "Patient.name[0].given[0]");

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            var given = patched.Name[0].Given.ToList();
            Assert.Single(given);
            Assert.Equal("Fauntleroy", given[0]);
        }

        // ── move ────────────────────────────────────────────────────────────

        [Fact]
        public void move_first_name_to_second_position()
        {
            Patient patient = GetPatient(); // [Duck, Mouse]
            Parameters patch = MakeOp("move", "Patient.name", source: 0, destination: 1);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal(2, patched.Name.Count);
            Assert.Equal("Mouse", patched.Name[0].Family);
            Assert.Equal("Duck",  patched.Name[1].Family);
        }

        [Fact]
        public void move_second_name_to_first_position()
        {
            Patient patient = GetPatient(); // [Duck, Mouse]
            Parameters patch = MakeOp("move", "Patient.name", source: 1, destination: 0);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Equal("Mouse", patched.Name[0].Family);
            Assert.Equal("Duck",  patched.Name[1].Family);
        }

        [Fact]
        public void move_given_name_within_nested_array()
        {
            Patient patient = GetPatient(); // name[0].given = ["Donald","Fauntleroy"]
            Parameters patch = MakeOp("move", "Patient.name[0].given", source: 0, destination: 1);

            Resource result = _sut.Apply(patient, patch);

            Patient patched = Assert.IsType<Patient>(result);
            var given = patched.Name[0].Given.ToList();
            Assert.Equal("Fauntleroy", given[0]);
            Assert.Equal("Donald",     given[1]);
        }

        // ── multiple sequential operations ──────────────────────────────────

        [Fact]
        public void multiple_operations_applied_in_sequence()
        {
            Patient patient = GetPatient(); // active = true, gender = male

            var p = new Parameters();
            void AddOp(string type, string path, DataType value)
            {
                p.Parameter.Add(new Parameters.ParameterComponent
                {
                    Name = "operation",
                    Part =
                    [
                        new() { Name = "type",  Value = new Code(type) },
                        new() { Name = "path",  Value = new FhirString(path) },
                        new() { Name = "value", Value = value }
                    ]
                });
            }
            AddOp("replace", "Patient.active", new FhirBoolean(false));
            AddOp("replace", "Patient.gender", new Code("female"));

            Resource result = _sut.Apply(patient, p);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.False(patched.Active);
            Assert.Equal(AdministrativeGender.Female, patched.Gender);
        }

        [Fact]
        public void replace_then_delete_both_applied_in_order()
        {
            Patient patient = GetPatient(); // name[0].family = "Duck", two names

            var p = new Parameters();
            p.Parameter.Add(new Parameters.ParameterComponent
            {
                Name = "operation",
                Part =
                [
                    new() { Name = "type",  Value = new Code("replace") },
                    new() { Name = "path",  Value = new FhirString("Patient.name[0].family") },
                    new() { Name = "value", Value = new FhirString("Quack") }
                ]
            });
            p.Parameter.Add(new Parameters.ParameterComponent
            {
                Name = "operation",
                Part =
                [
                    new() { Name = "type", Value = new Code("delete") },
                    new() { Name = "path", Value = new FhirString("Patient.name[1]") }
                ]
            });

            Resource result = _sut.Apply(patient, p);

            Patient patched = Assert.IsType<Patient>(result);
            Assert.Single(patched.Name);
            Assert.Equal("Quack", patched.Name[0].Family);
        }

        [Fact]
        public void empty_operations_list_throws_FhirErrorException()
        {
            Patient patient = GetPatient();
            var patch = new Parameters(); // no operations

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        // ── error paths ─────────────────────────────────────────────────────

        [Fact]
        public void unknown_operation_type_throws_FhirErrorException()
        {
            Patient patient = GetPatient();
            Parameters patch = MakeOp("explode", "Patient.active");

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
            Assert.Contains("explode", ex.Message);
        }

        [Fact]
        public void path_not_found_throws_FhirErrorException()
        {
            Patient patient = GetPatient();
            Parameters patch = MakeOp("delete", "Patient.nonExistentProperty");

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void path_index_out_of_range_throws_FhirErrorException()
        {
            Patient patient = GetPatient(); // only name[0] and name[1]
            Parameters patch = MakeOp("delete", "Patient.name[99]");

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void add_without_name_part_throws_FhirErrorException()
        {
            Patient patient = GetPatient();
            var patch = new Parameters();
            patch.Parameter.Add(new Parameters.ParameterComponent
            {
                Name = "operation",
                Part =
                [
                    new() { Name = "type",  Value = new Code("add") },
                    new() { Name = "path",  Value = new FhirString("Patient") },
                    new() { Name = "value", Value = new HumanName { Family = "Test" } }
                ]
            });

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void add_without_value_part_throws_FhirErrorException()
        {
            Patient patient = GetPatient();
            var patch = new Parameters();
            patch.Parameter.Add(new Parameters.ParameterComponent
            {
                Name = "operation",
                Part =
                [
                    new() { Name = "type", Value = new Code("add") },
                    new() { Name = "path", Value = new FhirString("Patient") },
                    new() { Name = "name", Value = new FhirString("name") }
                    // no value part
                ]
            });

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void insert_without_index_part_throws_FhirErrorException()
        {
            Patient patient = GetPatient();
            var patch = new Parameters();
            patch.Parameter.Add(new Parameters.ParameterComponent
            {
                Name = "operation",
                Part =
                [
                    new() { Name = "type",  Value = new Code("insert") },
                    new() { Name = "path",  Value = new FhirString("Patient.name") },
                    new() { Name = "value", Value = new HumanName { Family = "Test" } }
                    // no index part
                ]
            });

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void insert_index_out_of_range_throws_FhirErrorException()
        {
            Patient patient = GetPatient(); // 2 names; valid insert range 0..2
            Parameters patch = MakeOp("insert", "Patient.name", value: new HumanName { Family = "X" }, index: 99);

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void replace_without_value_part_throws_FhirErrorException()
        {
            Patient patient = GetPatient();
            var patch = new Parameters();
            patch.Parameter.Add(new Parameters.ParameterComponent
            {
                Name = "operation",
                Part =
                [
                    new() { Name = "type", Value = new Code("replace") },
                    new() { Name = "path", Value = new FhirString("Patient.active") }
                    // no value part
                ]
            });

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void move_without_source_part_throws_FhirErrorException()
        {
            Patient patient = GetPatient();
            var patch = new Parameters();
            patch.Parameter.Add(new Parameters.ParameterComponent
            {
                Name = "operation",
                Part =
                [
                    new() { Name = "type",        Value = new Code("move") },
                    new() { Name = "path",        Value = new FhirString("Patient.name") },
                    new() { Name = "destination", Value = new Integer(1) }
                    // no source part
                ]
            });

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void move_source_index_out_of_range_throws_FhirErrorException()
        {
            Patient patient = GetPatient(); // 2 names: indices 0,1
            Parameters patch = MakeOp("move", "Patient.name", source: 99, destination: 0);

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void move_destination_index_out_of_range_throws_FhirErrorException()
        {
            Patient patient = GetPatient(); // 2 names: indices 0,1
            Parameters patch = MakeOp("move", "Patient.name", source: 0, destination: 99);

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }

        [Fact]
        public void ambiguous_path_without_index_throws_FhirErrorException()
        {
            // Patient.name matches 2 elements; without [n] indexing it is ambiguous for delete
            Patient patient = GetPatient();
            Parameters patch = MakeOp("delete", "Patient.name"); // ambiguous — 2 names

            FhirErrorException ex = Assert.Throws<FhirErrorException>(() => _sut.Apply(patient, patch));
            Assert.Equal(HttpStatusCode.BadRequest, ex.HttpStatusCode);
        }
    }
}
